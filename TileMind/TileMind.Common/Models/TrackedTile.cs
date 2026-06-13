using OpenCvSharp;

namespace TileMind.Common.Models;

/// <summary>
/// 跨帧追踪的麻将牌，拥有全局唯一 TrackId。
/// </summary>
public class TrackedTile
{
    public long TrackId { get; set; }
    public TileType TileType { get; set; }
    public float Confidence { get; set; }
    public Rect BoundingBox { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public int ConsecutiveMisses { get; set; }

    /// <summary>牌的来源：null = 从牌山摸入，有值 = 从该玩家弃牌处获取（被吃/碰/杠）。</summary>
    public SeatPosition? SourcePlayer { get; set; }

    public static TrackedTile FromDetection(DetectionResult detection, long trackId)
    {
        return new TrackedTile
        {
            TrackId = trackId,
            TileType = detection.TileType,
            Confidence = detection.Confidence,
            BoundingBox = detection.BoundingBox,
            FirstSeen = DateTime.UtcNow,
            LastSeen = DateTime.UtcNow,
            ConsecutiveMisses = 0
        };
    }

    public void UpdateFromDetection(DetectionResult detection)
    {
        TileType = detection.TileType;
        Confidence = detection.Confidence;
        BoundingBox = detection.BoundingBox;
        LastSeen = DateTime.UtcNow;
        ConsecutiveMisses = 0;
    }
}
