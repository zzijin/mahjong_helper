using Microsoft.Extensions.Options;
using TileMind.Common.Config;
using TileMind.Common.Models;

namespace TileMind.Core.Services;

/// <summary>
/// 单帧静态分析服务：分离手牌/副露、判定副露类型。
/// 不依赖任何跨帧状态，在追踪/非追踪模式下均可使用。
/// </summary>
public class FrameAnalyzerService
{
    private readonly HandMeldSeparator _separator;

    public FrameAnalyzerService(IOptionsSnapshot<GameStateTrackerOptions> options)
    {
        _separator = new HandMeldSeparator(options.Value);
    }

    /// <summary>
    /// 对一帧检测结果执行静态分析，输出 AnalyzedFrame。
    /// </summary>
    public AnalyzedFrame Analyze(FrameDetections input)
    {
        var result = new AnalyzedFrame
        {
            Timestamp = input.Timestamp,
            DoraIndicatorDetections = input.DoraIndicatorDetections,
            DiscardPondDetections = input.DiscardPondDetections
        };

        // 宝牌指示牌 → 实际宝牌映射
        result.DoraTiles = MapDoraTiles(input.DoraIndicatorDetections);

        foreach (SeatPosition seat in Enum.GetValues<SeatPosition>())
        {
            input.HandAndMeldDetections.TryGetValue(seat, out var handMeldDets);
            handMeldDets ??= new();

            var (handDets, meldGroups) = _separator.Separate(handMeldDets, seat);

            var melds = meldGroups
                .Where(g => g.Count >= 2)
                .Select(g => new MeldAnalysis
                {
                    MeldType = DetermineMeldType(g, seat),
                    Tiles = g
                }).ToList();

            result.DiscardPondDetections.TryGetValue(seat, out var pondDets);
            pondDets ??= new();

            result.Players[seat] = new PlayerFrameAnalysis
            {
                Seat = seat,
                HandTiles = handDets,
                Melds = melds,
                HasRiichiDiscard = DetectRiichi(seat, pondDets, out var riichiTile),
                RiichiDiscardTile = riichiTile
            };
        }

        // 判定活跃玩家
        result.ActivePlayer = DetermineActivePlayer(result.Players);

        return result;
    }

    /// <summary>
    /// 根据规则判定活跃玩家：手牌+副露总数 = 14 + 杠数。
    /// 若无人精确满足，取总数最大的玩家；若仍无法判定返回 null。
    /// </summary>
    private static SeatPosition? DetermineActivePlayer(Dictionary<SeatPosition, PlayerFrameAnalysis> players)
    {
        SeatPosition? bestSeat = null;
        int bestTotal = -1;
        bool exactMatch = false;

        foreach (var (seat, player) in players)
        {
            int total = player.HandTiles.Count + player.Melds.Sum(m => m.Tiles.Count);
            int kanCount = player.Melds.Count(m =>
                m.MeldType is MeldType.Kan or MeldType.Ankan or MeldType.Kakan);
            int expected = 14 + kanCount;

            if (total == expected)
            {
                if (!exactMatch) { bestSeat = seat; bestTotal = total; exactMatch = true; }
                // 多个精确匹配 → 先到先得，后续可改为取 total 最大
            }
            else if (!exactMatch && total > bestTotal)
            {
                bestSeat = seat;
                bestTotal = total;
            }
        }

        return bestSeat;
    }

    /// <summary>
    /// 立直检测：根据弃牌区检测框宽高比按座位方向判断。
    /// 自家/对家：正常 h>w（竖），立直 w>h（横）。
    /// 上家/下家：正常 w>h（横），立直 h>w（竖）。
    /// </summary>
    private static bool DetectRiichi(SeatPosition seat, List<DetectionResult> pondDets, out DetectionResult? riichiTile)
    {
        riichiTile = null;
        foreach (var det in pondDets)
        {
            float ratio = (float)det.BoundingBox.Width / det.BoundingBox.Height;
            bool isRotated = seat is SeatPosition.Self or SeatPosition.Opposite
                ? ratio > 1.0f   // 横置：宽 > 高
                : ratio < 1.0f;  // 竖置：高 > 宽

            if (isRotated)
            {
                riichiTile = det;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 根据牌数和花色判定副露类型。
    /// </summary>
    internal static MeldType DetermineMeldType(List<DetectionResult> detections, SeatPosition seat)
    {
        if (detections.Count == 4) return MeldType.Kan;
        if (detections.Count != 3) return MeldType.Pon;

        var types = detections
            .Select(d => ActionClassifier.NormalizeTileType(d.TileType))
            .ToList();

        return ActionClassifier.IsChi(types) ? MeldType.Chi : MeldType.Pon;
    }

    /// <summary>
    /// 将宝牌指示牌检测结果映射为实际宝牌 TileType（去重）。
    /// </summary>
    private static List<TileType> MapDoraTiles(List<DetectionResult> doraIndicatorDets)
    {
        var doraTiles = new HashSet<TileType>();
        foreach (var det in doraIndicatorDets)
        {
            var dora = GetDoraTileType(det.TileType);
            if (dora != TileType.Unknown)
                doraTiles.Add(dora);
        }
        return doraTiles.ToList();
    }

    /// <summary>
    /// 日麻规则：从指示牌推算宝牌。
    /// 数牌 1→2→…→8→9→1，风牌 东→南→西→北→东，三元牌 白→发→中→白。
    /// 赤牌（0）视为对应数字 5 处理。
    /// </summary>
    internal static TileType GetDoraTileType(TileType indicator)
    {
        // 赤牌归一化为数字 5
        int normalized = ActionClassifier.NormalizeTileType(indicator);
        int suit = normalized / 10;
        int num = normalized % 10;

        if (suit <= 2) // 万/筒/索
        {
            int doraNum = num == 9 ? 1 : num + 1;
            return (TileType)(suit * 10 + doraNum);
        }
        else if (suit == 3) // 字牌
        {
            int doraNum;
            if (num <= 4) // 风牌 1-4
                doraNum = (num % 4) + 1;
            else // 三元牌 5-7
                doraNum = ((num - 5 + 1) % 3) + 5;
            return (TileType)(suit * 10 + doraNum);
        }

        return TileType.Unknown;
    }
}
