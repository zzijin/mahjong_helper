namespace TileMind.Common.Models;

/// <summary>
/// 一次检测到的游戏动作（摸牌/出牌/吃/碰/杠等）。
/// </summary>
public record class MahjongAction
{
    public ActionType ActionType { get; init; }
    public SeatPosition Player { get; init; }
    public List<TrackedTile> Tiles { get; init; } = new();
    public SeatPosition? RelatedPlayer { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
