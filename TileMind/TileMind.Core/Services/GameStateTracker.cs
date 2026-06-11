using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TileMind.Common.Config;
using TileMind.Common.Models;

namespace TileMind.Core.Services;

/// <summary>
/// 主编排器：接收每帧的检测结果，编排 HandMeldSeparator → TileTracker → ActionClassifier，
/// 维护全局对局状态。
/// </summary>
public class GameStateTracker
{
    private readonly ILogger<GameStateTracker> _logger;
    private readonly GameStateTrackerOptions _options;
    private readonly HandMeldSeparator _separator;

    private readonly Dictionary<SeatPosition, TileTracker> _handTrackers = new();
    private readonly Dictionary<SeatPosition, TileTracker> _pondTrackers = new();

    private GameState _state = new();
    private bool _baselineEstablished;

    public GameState State => _state;

    public event Action<MahjongAction>? OnActionDetected;

    public GameStateTracker(IOptionsSnapshot<GameStateTrackerOptions> options, ILogger<GameStateTracker> logger)
    {
        _logger = logger;
        _options = options.Value;
        _separator = new HandMeldSeparator(_options);

        foreach (SeatPosition seat in Enum.GetValues<SeatPosition>())
        {
            _handTrackers[seat] = new TileTracker(_options.MatchIouThreshold, _options.MaxConsecutiveMisses);
            _pondTrackers[seat] = new TileTracker(_options.MatchIouThreshold, _options.MaxConsecutiveMisses);
        }
    }

    /// <summary>
    /// 处理一帧检测结果，返回本帧检测到的游戏动作列表。
    /// </summary>
    public List<MahjongAction> ProcessFrame(FrameDetections input)
    {
        _state.CurrentFrameNumber++;

        // 基线未建立 或 场面清空（弃牌区+副露区全空）→ 建立/重建基线
        if (!_baselineEstablished || IsBoardCleared(input))
        {
            if (_baselineEstablished)
            {
                _logger.LogInformation("检测到场面清空，重新建立基线。");
                Reset();
            }

            var initialActions = EstablishBaseline(input);
            foreach (var action in initialActions)
            {
                _state.ActionLog.Add(action);
                _logger.LogDebug("Action: {ActionType} by {Player}", action.ActionType, action.Player);
                OnActionDetected?.Invoke(action);
            }
            return initialActions;
        }

        var allDeltas = new Dictionary<SeatPosition, PlayerFrameDelta>();

        // 只处理实际存在的玩家，空缺座位不参与追踪
        foreach (var seat in _state.Players.Keys)
        {
            var playerState = _state.Players[seat];
            var delta = ProcessPlayerFrame(seat, input, playerState);
            allDeltas[seat] = delta;
        }

        // 处理宝牌指示区
        if (input.DoraIndicatorDetections.Count > 0)
            TrackDoraIndicators(input.DoraIndicatorDetections);

        // 分类动作
        var classifier = new ActionClassifier();
        var actions = classifier.Classify(allDeltas);

        // 记录动作日志
        foreach (var action in actions)
        {
            _state.ActionLog.Add(action);
            _logger.LogDebug("Action: {ActionType} by {Player}", action.ActionType, action.Player);
            OnActionDetected?.Invoke(action);
        }

        return actions;
    }

    private PlayerFrameDelta ProcessPlayerFrame(
        SeatPosition seat, FrameDetections input, PlayerState playerState)
    {
        // 获取该玩家的检测结果
        input.HandAndMeldDetections.TryGetValue(seat, out var handMeldDets);
        input.DiscardPondDetections.TryGetValue(seat, out var pondDets);
        handMeldDets ??= new();
        pondDets ??= new();

        // 1. 分离手牌和副露
        var (handDets, meldGroups) = _separator.Separate(handMeldDets, seat);

        // 2. 追踪手牌区域
        var handResult = _handTrackers[seat].Update(handDets, playerState.HandTiles);

        // 3. 追踪弃牌区域
        var pondResult = _pondTrackers[seat].Update(pondDets, playerState.DiscardPondTiles);

        // 4. 计算增量
        int prevHandCount = playerState.HandTiles.Count;
        int prevPondCount = playerState.DiscardPondTiles.Count;

        // 5. 检测新副露
        var newMelds = DetectNewMelds(meldGroups, playerState.MeldRecords, seat);

        // 6. 更新玩家状态
        playerState.HandTiles = handResult.ActiveTiles;
        playerState.DiscardPondTiles = pondResult.ActiveTiles;

        foreach (var meld in newMelds)
            playerState.MeldRecords.Add(meld);

        // 7. 检查 kakan（已有碰升级为杠）
        int upgradedMeldIndex = DetectKakan(meldGroups, playerState.MeldRecords);

        var delta = new PlayerFrameDelta
        {
            Seat = seat,
            HandAdded = handResult.NewTiles,
            HandRemoved = handResult.RemovedTiles,
            PondAdded = pondResult.NewTiles,
            PondRemoved = pondResult.RemovedTiles,
            MeldsAdded = upgradedMeldIndex < 0 ? newMelds : new(),
            PreviousHandCount = prevHandCount,
            CurrentHandCount = handResult.ActiveTiles.Count,
            PreviousPondCount = prevPondCount,
            CurrentPondCount = pondResult.ActiveTiles.Count,
            UpgradedMeldIndex = upgradedMeldIndex
        };

        return delta;
    }

    /// <summary>
    /// 检测新副露组：比较当前帧检测到的 meldGroups 与已有 MeldRecords。
    /// </summary>
    private List<MeldRecord> DetectNewMelds(
        List<List<DetectionResult>> meldGroups,
        List<MeldRecord> existingMelds,
        SeatPosition seat)
    {
        var newMelds = new List<MeldRecord>();

        foreach (var group in meldGroups)
        {
            if (group.Count < 2) continue;

            // 检查是否匹配已有副露（位置/类型重叠）
            bool isExisting = existingMelds.Any(m =>
                m.Tiles.Any(t => group.Any(d =>
                    CalculateIoU(t.BoundingBox, d.BoundingBox) > _options.MatchIouThreshold)));

            if (!isExisting)
            {
                var tiles = group.Select(d => TrackedTile.FromDetection(d, 0)).ToList();
                var meldType = DetermineMeldType(tiles, seat);
                newMelds.Add(new MeldRecord
                {
                    MeldType = meldType,
                    Tiles = tiles
                });
            }
        }

        return newMelds;
    }

    /// <summary>
    /// 检测加杠：已有3枚碰是否增长为4枚。
    /// </summary>
    private int DetectKakan(List<List<DetectionResult>> meldGroups, List<MeldRecord> existingMelds)
    {
        for (int i = 0; i < existingMelds.Count; i++)
        {
            var meld = existingMelds[i];
            if (meld.MeldType != MeldType.Pon || meld.Tiles.Count != 3) continue;

            // 检查是否有4枚组与现有碰重叠
            foreach (var group in meldGroups)
            {
                if (group.Count != 4) continue;

                int overlap = meld.Tiles.Count(t =>
                    group.Any(d => CalculateIoU(t.BoundingBox, d.BoundingBox) > _options.MatchIouThreshold));

                if (overlap >= 2)
                {
                    // 升级为杠
                    meld.MeldType = MeldType.Kakan;
                    meld.Tiles = group.Select(d => TrackedTile.FromDetection(d, 0)).ToList();
                    return i;
                }
            }
        }

        return -1;
    }

    private MeldType DetermineMeldType(List<TrackedTile> tiles, SeatPosition seat)
    {
        if (tiles.Count == 4) return MeldType.Kan;
        if (tiles.Count != 3) return MeldType.Pon;

        var types = tiles.Select(t => ActionClassifier.NormalizeTileType(t.TileType)).ToList();
        return ActionClassifier.IsChi(types) ? MeldType.Chi : MeldType.Pon;
    }

    /// <summary>
    /// 首帧基线建立：初始化有牌的玩家，识别对局初始状态。
    /// </summary>
    private List<MahjongAction> EstablishBaseline(FrameDetections input)
    {
        _state.Players.Clear();

        foreach (SeatPosition seat in Enum.GetValues<SeatPosition>())
        {
            input.HandAndMeldDetections.TryGetValue(seat, out var handMeldDets);
            input.DiscardPondDetections.TryGetValue(seat, out var pondDets);
            handMeldDets ??= new();
            pondDets ??= new();

            // 该座位没有任何检测结果 → 空缺，跳过
            if (handMeldDets.Count == 0 && pondDets.Count == 0)
                continue;

            var (handDets, meldGroups) = _separator.Separate(handMeldDets, seat);

            // 初始化追踪器
            var handResult = _handTrackers[seat].Update(handDets, new());
            var pondResult = _pondTrackers[seat].Update(pondDets, new());

            var playerState = new PlayerState { Seat = seat };
            playerState.HandTiles = handResult.ActiveTiles;
            playerState.DiscardPondTiles = pondResult.ActiveTiles;

            // 建立初始副露
            foreach (var group in meldGroups.Where(g => g.Count >= 2))
            {
                var tiles = group.Select(d => TrackedTile.FromDetection(d, 0)).ToList();
                playerState.MeldRecords.Add(new MeldRecord
                {
                    MeldType = DetermineMeldType(tiles, seat),
                    Tiles = tiles
                });
            }

            _state.Players[seat] = playerState;
        }

        _baselineEstablished = true;

        var playerCounts = string.Join(", ", _state.Players.Select(p => $"{p.Key}={p.Value.HandTiles.Count}手牌"));
        _logger.LogInformation("基线已建立: {PlayerCount}名玩家 [{Counts}]", _state.Players.Count, playerCounts);

        // 检测是否为对局初始状态
        return DetectInitialDeal();
    }

    /// <summary>
    /// 检测对局初始状态：所有玩家弃牌区空、副露空 → 配牌完毕，产生初始摸牌动作。
    /// </summary>
    private List<MahjongAction> DetectInitialDeal()
    {
        var actions = new List<MahjongAction>();

        if (_state.Players.Count == 0)
            return actions;

        // 任一玩家有弃牌或副露 → 非初始状态
        bool hasDiscardsOrMelds = _state.Players.Values.Any(p =>
            p.DiscardPondTiles.Count > 0 || p.MeldRecords.Count > 0);
        if (hasDiscardsOrMelds)
            return actions;

        // 确定庄家（手牌 14 张的玩家）
        SeatPosition? dealerSeat = null;
        foreach (var (seat, player) in _state.Players)
        {
            if (player.HandTiles.Count == 14)
            {
                dealerSeat = seat;
                break;
            }
        }

        if (dealerSeat.HasValue)
            _state.DealerSeat = dealerSeat.Value;

        // 为每个玩家生成初始摸牌动作（每人 13 张，庄家额外 1 张）
        foreach (var (seat, player) in _state.Players)
        {
            if (player.HandTiles.Count == 0) continue;

            // 前 13 张手牌
            var firstThirteen = player.HandTiles.Take(13).ToList();
            if (firstThirteen.Count > 0)
            {
                actions.Add(new MahjongAction
                {
                    ActionType = ActionType.Draw,
                    Player = seat,
                    Tiles = firstThirteen
                });
            }

            // 庄家额外摸入第 14 张
            if (seat == dealerSeat && player.HandTiles.Count > 13)
            {
                actions.Add(new MahjongAction
                {
                    ActionType = ActionType.Draw,
                    Player = seat,
                    Tiles = player.HandTiles.Skip(13).ToList()
                });
            }
        }

        _logger.LogInformation("检测到对局初始状态，{PlayerCount}名玩家，庄家={Dealer}",
            _state.Players.Count, dealerSeat);

        return actions;
    }

    /// <summary>
    /// 判断当前帧是否场面清空：所有方向的弃牌区均无检测结果，
    /// 且至少存在手牌+副露区检测（避免空白帧误触发）。
    /// </summary>
    private static bool IsBoardCleared(FrameDetections input)
    {
        bool hasHandMeldTiles = false;

        foreach (SeatPosition seat in Enum.GetValues<SeatPosition>())
        {
            input.DiscardPondDetections.TryGetValue(seat, out var pondDets);
            input.HandAndMeldDetections.TryGetValue(seat, out var handMeldDets);

            if ((pondDets?.Count ?? 0) > 0) return false;
            if ((handMeldDets?.Count ?? 0) > 0) hasHandMeldTiles = true;
        }

        return hasHandMeldTiles;
    }

    private void TrackDoraIndicators(List<DetectionResult> doraDets)
    {
        // 简单的去重追踪
        foreach (var det in doraDets)
        {
            bool exists = _state.DoraIndicators.Any(t =>
                CalculateIoU(t.BoundingBox, det.BoundingBox) > _options.MatchIouThreshold);
            if (!exists)
                _state.DoraIndicators.Add(TrackedTile.FromDetection(det, 0));
        }
    }

    /// <summary>
    /// 重置所有状态。
    /// </summary>
    public void Reset()
    {
        _state = new GameState();
        _baselineEstablished = false;
        foreach (var tracker in _handTrackers.Values) tracker.Reset();
        foreach (var tracker in _pondTrackers.Values) tracker.Reset();
    }

    private static float CalculateIoU(OpenCvSharp.Rect a, OpenCvSharp.Rect b)
    {
        var intersection = OpenCvSharp.Rect.Intersect(a, b);
        if (intersection.Width <= 0 || intersection.Height <= 0) return 0;
        float ia = intersection.Width * intersection.Height;
        float ua = (a.Width * a.Height) + (b.Width * b.Height) - ia;
        return ia / ua;
    }
}
