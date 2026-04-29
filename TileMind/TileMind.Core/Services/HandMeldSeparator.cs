using TileMind.Common.Config;
using TileMind.Common.Models;

namespace TileMind.Core.Services;

/// <summary>
/// 手牌/副露分离器：从合并的 Hand+Meld 区域检测结果中，
/// 通过空间聚类（gap analysis）分离手牌和副露组。
/// </summary>
public class HandMeldSeparator
{
    private readonly GameStateTrackerOptions _options;

    public HandMeldSeparator(GameStateTrackerOptions options)
    {
        _options = options;
    }

    public HandMeldSeparator() : this(new GameStateTrackerOptions()) { }

    /// <summary>
    /// 分离指定玩家的手牌和副露组。
    /// </summary>
    public (List<DetectionResult> HandTiles, List<List<DetectionResult>> MeldGroups) Separate(
        List<DetectionResult> detections, SeatPosition seat)
    {
        if (detections.Count == 0)
            return (new(), new());

        var isHorizontal = seat is SeatPosition.Self or SeatPosition.Opposite;
        var meldsAfterHand = seat is SeatPosition.Self or SeatPosition.Right;

        var items = detections.Select(d => new TileInfo
        {
            Detection = d,
            Primary = isHorizontal ? d.BoundingBox.X + d.BoundingBox.Width / 2.0
                                   : d.BoundingBox.Y + d.BoundingBox.Height / 2.0,
            Secondary = isHorizontal ? d.BoundingBox.Y + d.BoundingBox.Height / 2.0
                                     : d.BoundingBox.X + d.BoundingBox.Width / 2.0,
            Width = (double)d.BoundingBox.Width,
            Height = (double)d.BoundingBox.Height
        }).ToList();

        return isHorizontal
            ? SeparateByAxis(items, meldsAfterHand, useHorizontal: true)
            : SeparateByAxis(items, meldsAfterHand, useHorizontal: false);
    }

    private (List<DetectionResult>, List<List<DetectionResult>>) SeparateByAxis(
        List<TileInfo> items, bool meldsAfterHand, bool useHorizontal)
    {
        // 沿副轴聚类 → 找出各行/列
        var rows = ClusterByCoordinate(items, t => t.Secondary, _options.RowTolerance);

        // 找出 tile 数最多的行 = 手牌行
        var handRow = rows.OrderByDescending(r => r.Count).First();

        // 在手牌行内沿主轴排序
        handRow.Sort((a, b) => a.Primary.CompareTo(b.Primary));

        // 计算相邻间距，找出最大 gap
        double avgTileSize = useHorizontal
            ? handRow.Average(t => t.Width)
            : handRow.Average(t => t.Height);

        int splitIndex = FindHandMeldSplit(handRow, _options.HandMeldGapMultiplier);

        List<DetectionResult> handTiles;
        List<TileInfo> meldCandidates;

        if (splitIndex < 0)
        {
            // 无清晰 gap，全部归入手牌
            handTiles = items.Select(t => t.Detection).ToList();
            return (handTiles, new());
        }

        if (meldsAfterHand)
        {
            handTiles = handRow.Take(splitIndex + 1).Select(t => t.Detection).ToList();
            meldCandidates = handRow.Skip(splitIndex + 1).ToList();
        }
        else
        {
            handTiles = handRow.Skip(splitIndex + 1).Select(t => t.Detection).ToList();
            meldCandidates = handRow.Take(splitIndex + 1).ToList();
        }

        // 收集非手牌行的 tile → 加入副露候选
        foreach (var row in rows.Where(r => r != handRow))
            meldCandidates.AddRange(row);

        // 聚类副露候选 → 副露组
        var meldGroups = ClusterMeldCandidates(meldCandidates, useHorizontal);

        return (handTiles, meldGroups);
    }

    /// <summary>
    /// 在手牌行内找到手牌与副露的分界点。
    /// 返回 gap 的索引（gap 在 index 和 index+1 之间）。
    /// 若无显著 gap 返回 -1。
    /// </summary>
    private static int FindHandMeldSplit(List<TileInfo> handRow, double gapMultiplier)
    {
        if (handRow.Count < 2) return -1;

        var gaps = new List<double>();
        for (int i = 0; i < handRow.Count - 1; i++)
            gaps.Add(handRow[i + 1].Primary - handRow[i].Primary);

        if (gaps.Count == 0) return -1;

        double avgGap = gaps.Average();
        double maxGap = gaps.Max();

        // 最大 gap 必须显著大于平均 gap
        if (maxGap > avgGap * gapMultiplier)
            return gaps.IndexOf(maxGap);

        return -1;
    }

    /// <summary>
    /// 将副露候选 tile 聚类为 2-4 个一组的副露组。
    /// </summary>
    private List<List<DetectionResult>> ClusterMeldCandidates(List<TileInfo> candidates, bool useHorizontal)
    {
        if (candidates.Count == 0) return new();

        // 沿主轴排序后聚类
        candidates.Sort((a, b) => a.Primary.CompareTo(b.Primary));
        var clusters = ClusterByCoordinate(candidates, t => t.Primary, _options.MeldProximityThreshold);

        var meldGroups = new List<List<DetectionResult>>();

        foreach (var cluster in clusters)
        {
            var dets = cluster.Select(c => c.Detection).ToList();
            if (dets.Count is >= 2 and <= 4)
                meldGroups.Add(dets);
            // 单 tile 簇 → 可能为噪音，忽略
        }

        return meldGroups;
    }

    /// <summary>
    /// 按坐标值对 tile 进行一维聚类。
    /// </summary>
    private static List<List<T>> ClusterByCoordinate<T>(
        List<T> items, Func<T, double> coordSelector, double tolerance)
    {
        if (items.Count == 0) return new();

        var sorted = items.OrderBy(coordSelector).ToList();
        var clusters = new List<List<T>>();
        var current = new List<T> { sorted[0] };

        for (int i = 1; i < sorted.Count; i++)
        {
            if (coordSelector(sorted[i]) - coordSelector(sorted[i - 1]) > tolerance)
            {
                clusters.Add(current);
                current = new List<T>();
            }
            current.Add(sorted[i]);
        }
        clusters.Add(current);

        return clusters;
    }

    private class TileInfo
    {
        public DetectionResult Detection { get; set; } = null!;
        public double Primary { get; set; }
        public double Secondary { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }
}
