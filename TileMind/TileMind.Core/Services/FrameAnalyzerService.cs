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

            result.Players[seat] = new PlayerFrameAnalysis
            {
                Seat = seat,
                HandTiles = handDets,
                Melds = melds
            };
        }

        return result;
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
