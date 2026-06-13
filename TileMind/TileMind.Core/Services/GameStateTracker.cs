using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TileMind.Common.Config;
using TileMind.Common.Models;

namespace TileMind.Core.Services;

/// <summary>
/// 主编排器：接收静态分析后的 AnalyzedFrame，编排 TileTracker → ActionClassifier，
/// 维护全局对局状态。不在此层做手牌/副露分离（已在 FrameAnalyzerService 完成）。
/// </summary>
public class GameStateTracker
{
    private readonly ILogger<GameStateTracker> _logger;
    private readonly GameStateTrackerOptions _options;

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

        foreach (SeatPosition seat in Enum.GetValues<SeatPosition>())
        {
            _handTrackers[seat] = new TileTracker(_options.MatchIouThreshold, _options.MaxConsecutiveMisses);
            _pondTrackers[seat] = new TileTracker(_options.MatchIouThreshold, _options.MaxConsecutiveMisses);
        }
    }

    /// <summary>
    /// 处理一帧静态分析结果，返回本帧检测到的游戏动作列表。
    /// </summary>
    public List<MahjongAction> ProcessFrame(AnalyzedFrame input)
    {
        _state.CurrentFrameNumber++;

        // 基线未建立 或 场面清空 → 建立/重建基线
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

        // 只处理实际存在的玩家
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
        var actions = classifier.Classify(allDeltas, _state.CurrentFrameNumber);

        foreach (var action in actions)
        {
            _state.ActionLog.Add(action);
            _logger.LogDebug("Action: {ActionType} by {Player}", action.ActionType, action.Player);
            OnActionDetected?.Invoke(action);
        }

        return actions;
    }

    private PlayerFrameDelta ProcessPlayerFrame(
        SeatPosition seat, AnalyzedFrame input, PlayerState playerState)
    {
        input.Players.TryGetValue(seat, out var analysis);
        input.DiscardPondDetections.TryGetValue(seat, out var pondDets);
        var handDets = analysis?.HandTiles ?? new();
        var melds = analysis?.Melds ?? new();
        pondDets ??= new();

        // 1. 追踪手牌区域
        var handResult = _handTrackers[seat].Update(handDets, playerState.HandTiles);

        // 2. 追踪弃牌区域
        var pondResult = _pondTrackers[seat].Update(pondDets, playerState.DiscardPondTiles);

        // 3. 计算增量
        int prevHandCount = playerState.HandTiles.Count;
        int prevPondCount = playerState.DiscardPondTiles.Count;

        int pondDiff = pondResult.ActiveTiles.Count - prevPondCount;
        // 牌河数量下降 ≥ 2 → 错误帧，跳过该玩家（TODO: 待进一步思考阈值）
        if (pondDiff <= -2)
        {
            _logger.LogWarning("玩家 {Seat} 牌河数量异常下降 {Diff}（{Prev}→{Curr}），跳过本帧处理。",
                seat, pondDiff, prevPondCount, pondResult.ActiveTiles.Count);
            return new PlayerFrameDelta { Seat = seat };
        }

        // 4. 检测新副露（与已有 MeldRecords 对比）
        var newMelds = DetectNewMelds(melds, playerState.MeldRecords, seat);

        // 5. 更新玩家状态
        playerState.HandTiles = handResult.ActiveTiles;
        playerState.DiscardPondTiles = pondResult.ActiveTiles;

        foreach (var meld in newMelds)
            playerState.MeldRecords.Add(meld);

        // 6. 检查 kakan
        int upgradedMeldIndex = DetectKakan(melds, playerState.MeldRecords);

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
    /// 检测新副露组：对比当前帧的 MeldAnalysis 与已有 MeldRecords。
    /// </summary>
    private List<MeldRecord> DetectNewMelds(
        List<MeldAnalysis> currentMelds,
        List<MeldRecord> existingMelds,
        SeatPosition seat)
    {
        var newMelds = new List<MeldRecord>();

        foreach (var meld in currentMelds)
        {
            bool isExisting = existingMelds.Any(m =>
                m.Tiles.Any(t => meld.Tiles.Any(d =>
                    CalculateIoU(t.BoundingBox, d.BoundingBox) > _options.MatchIouThreshold)));

            if (!isExisting)
            {
                var tiles = meld.Tiles.Select(d => TrackedTile.FromDetection(d, 0)).ToList();
                newMelds.Add(new MeldRecord
                {
                    MeldType = meld.MeldType,
                    Tiles = tiles
                });
            }
        }

        return newMelds;
    }

    /// <summary>
    /// 检测加杠：已有3枚碰是否增长为4枚。
    /// </summary>
    private int DetectKakan(List<MeldAnalysis> currentMelds, List<MeldRecord> existingMelds)
    {
        for (int i = 0; i < existingMelds.Count; i++)
        {
            var meld = existingMelds[i];
            if (meld.MeldType != MeldType.Pon || meld.Tiles.Count != 3) continue;

            foreach (var group in currentMelds)
            {
                if (group.Tiles.Count != 4) continue;

                int overlap = meld.Tiles.Count(t =>
                    group.Tiles.Any(d => CalculateIoU(t.BoundingBox, d.BoundingBox) > _options.MatchIouThreshold));

                if (overlap >= 2)
                {
                    meld.MeldType = MeldType.Kakan;
                    meld.Tiles = group.Tiles.Select(d => TrackedTile.FromDetection(d, 0)).ToList();
                    return i;
                }
            }
        }

        return -1;
    }

    /// <summary>
    /// 首帧基线建立：初始化有牌的玩家，识别对局初始状态。
    /// </summary>
    private List<MahjongAction> EstablishBaseline(AnalyzedFrame input)
    {
        _state.Players.Clear();

        foreach (SeatPosition seat in Enum.GetValues<SeatPosition>())
        {
            input.Players.TryGetValue(seat, out var analysis);
            input.DiscardPondDetections.TryGetValue(seat, out var pondDets);
            var handDets = analysis?.HandTiles ?? new();
            var melds = analysis?.Melds ?? new();
            pondDets ??= new();

            // 该座位没有任何检测结果 → 空缺，跳过
            if (handDets.Count == 0 && pondDets.Count == 0 && melds.Count == 0)
                continue;

            var handResult = _handTrackers[seat].Update(handDets, new());
            var pondResult = _pondTrackers[seat].Update(pondDets, new());

            var playerState = new PlayerState { Seat = seat };
            playerState.HandTiles = handResult.ActiveTiles;
            playerState.DiscardPondTiles = pondResult.ActiveTiles;

            foreach (var meld in melds)
            {
                var tiles = meld.Tiles.Select(d => TrackedTile.FromDetection(d, 0)).ToList();
                playerState.MeldRecords.Add(new MeldRecord
                {
                    MeldType = meld.MeldType,
                    Tiles = tiles
                });
            }

            _state.Players[seat] = playerState;
        }

        _baselineEstablished = true;

        var playerCounts = string.Join(", ", _state.Players.Select(p => $"{p.Key}={p.Value.HandTiles.Count}手牌"));
        _logger.LogInformation("基线已建立: {PlayerCount}名玩家 [{Counts}]", _state.Players.Count, playerCounts);

        return DetectInitialDeal();
    }

    private List<MahjongAction> DetectInitialDeal()
    {
        var actions = new List<MahjongAction>();

        if (_state.Players.Count == 0)
            return actions;

        bool hasDiscardsOrMelds = _state.Players.Values.Any(p =>
            p.DiscardPondTiles.Count > 0 || p.MeldRecords.Count > 0);
        if (hasDiscardsOrMelds)
            return actions;

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

        foreach (var (seat, player) in _state.Players)
        {
            if (player.HandTiles.Count == 0) continue;

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

    private static bool IsBoardCleared(AnalyzedFrame input)
    {
        bool hasHandTiles = false;

        foreach (SeatPosition seat in Enum.GetValues<SeatPosition>())
        {
            input.DiscardPondDetections.TryGetValue(seat, out var pondDets);
            input.Players.TryGetValue(seat, out var analysis);

            if ((pondDets?.Count ?? 0) > 0) return false;
            if ((analysis?.Melds.Count ?? 0) > 0) return false;
            if ((analysis?.HandTiles.Count ?? 0) > 0) hasHandTiles = true;
        }

        return hasHandTiles;
    }

    private void TrackDoraIndicators(List<DetectionResult> doraDets)
    {
        foreach (var det in doraDets)
        {
            bool exists = _state.DoraIndicators.Any(t =>
                CalculateIoU(t.BoundingBox, det.BoundingBox) > _options.MatchIouThreshold);
            if (!exists)
                _state.DoraIndicators.Add(TrackedTile.FromDetection(det, 0));
        }
    }

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
