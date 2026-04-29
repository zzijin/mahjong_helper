namespace TileMind.Common.Models;

/// <summary>
/// 单个玩家的当前追踪状态。
/// </summary>
public class PlayerState
{
    public SeatPosition Seat { get; set; }
    public List<TrackedTile> HandTiles { get; set; } = new();
    public List<TrackedTile> DiscardPondTiles { get; set; } = new();
    public List<MeldRecord> MeldRecords { get; set; } = new();
    public bool IsRiichi { get; set; }
}
