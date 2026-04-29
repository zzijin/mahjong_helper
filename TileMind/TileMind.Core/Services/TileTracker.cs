using OpenCvSharp;
using TileMind.Common.Models;

namespace TileMind.Core.Services;

/// <summary>
/// 帧间 tile 匹配器：通过 IoU 将当前帧检测与上一帧已追踪 tile 匹配，
/// 管理 TrackedTile 的生命周期和全局 TrackId。
/// </summary>
public class TileTracker
{
    private long _nextTrackId = 1;
    private readonly float _iouThreshold;
    private readonly int _maxMisses;

    public TileTracker(float iouThreshold = 0.45f, int maxMisses = 3)
    {
        _iouThreshold = iouThreshold;
        _maxMisses = maxMisses;
    }

    public long NextTrackId => _nextTrackId;

    /// <summary>
    /// 将当前帧检测结果与上一帧已追踪 tile 匹配并更新状态。
    /// </summary>
    public TrackingResult Update(
        List<DetectionResult> currentDetections,
        List<TrackedTile> previousTiles)
    {
        var activeTiles = new List<TrackedTile>();
        var newTiles = new List<TrackedTile>();
        var removedTiles = new List<TrackedTile>();

        if (currentDetections.Count == 0)
        {
            // 当前帧无检测：所有上一帧 tile 累计 miss
            foreach (var tile in previousTiles)
            {
                tile.ConsecutiveMisses++;
                if (tile.ConsecutiveMisses >= _maxMisses)
                    removedTiles.Add(tile);
                else
                    activeTiles.Add(tile);
            }
            return new TrackingResult { ActiveTiles = activeTiles, NewTiles = newTiles, RemovedTiles = removedTiles };
        }

        var matchedPrevIndices = new HashSet<int>();
        var matchedCurrIndices = new HashSet<int>();

        // 构建所有 detection-trackedTile 对的 IoU，按 IoU 降序贪心匹配
        var pairs = new List<(int detIdx, int tileIdx, float iou)>();
        for (int i = 0; i < currentDetections.Count; i++)
        {
            for (int j = 0; j < previousTiles.Count; j++)
            {
                float iou = CalculateIoU(currentDetections[i].BoundingBox, previousTiles[j].BoundingBox);
                if (iou >= _iouThreshold)
                    pairs.Add((i, j, iou));
            }
        }

        pairs.Sort((a, b) => b.iou.CompareTo(a.iou));

        foreach (var (detIdx, tileIdx, iou) in pairs)
        {
            if (matchedCurrIndices.Contains(detIdx) || matchedPrevIndices.Contains(tileIdx))
                continue;

            matchedCurrIndices.Add(detIdx);
            matchedPrevIndices.Add(tileIdx);

            previousTiles[tileIdx].UpdateFromDetection(currentDetections[detIdx]);
            activeTiles.Add(previousTiles[tileIdx]);
        }

        // 未匹配的当前帧检测 → 新 tile
        for (int i = 0; i < currentDetections.Count; i++)
        {
            if (matchedCurrIndices.Contains(i)) continue;
            var newTile = TrackedTile.FromDetection(currentDetections[i], _nextTrackId++);
            activeTiles.Add(newTile);
            newTiles.Add(newTile);
        }

        // 未匹配的上一帧 tile → 累计 miss
        for (int j = 0; j < previousTiles.Count; j++)
        {
            if (matchedPrevIndices.Contains(j)) continue;
            previousTiles[j].ConsecutiveMisses++;
            if (previousTiles[j].ConsecutiveMisses >= _maxMisses)
                removedTiles.Add(previousTiles[j]);
            else
                activeTiles.Add(previousTiles[j]);
        }

        return new TrackingResult { ActiveTiles = activeTiles, NewTiles = newTiles, RemovedTiles = removedTiles };
    }

    public void Reset()
    {
        _nextTrackId = 1;
    }

    private static float CalculateIoU(Rect a, Rect b)
    {
        var intersection = Rect.Intersect(a, b);
        if (intersection.Width <= 0 || intersection.Height <= 0)
            return 0;

        float intersectionArea = intersection.Width * intersection.Height;
        float unionArea = (a.Width * a.Height) + (b.Width * b.Height) - intersectionArea;
        return intersectionArea / unionArea;
    }
}

/// <summary>
/// 单次追踪更新的结果。
/// </summary>
public class TrackingResult
{
    public List<TrackedTile> ActiveTiles { get; set; } = new();
    public List<TrackedTile> NewTiles { get; set; } = new();
    public List<TrackedTile> RemovedTiles { get; set; } = new();
}
