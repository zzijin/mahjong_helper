namespace TileMind.Common.Models;

/// <summary>
/// 副露记录：一次吃/碰/杠的完整信息。
/// </summary>
public class MeldRecord
{
    public MeldType MeldType { get; set; }
    public List<TrackedTile> Tiles { get; set; } = new();
    public SeatPosition? StolenFrom { get; set; }
}
