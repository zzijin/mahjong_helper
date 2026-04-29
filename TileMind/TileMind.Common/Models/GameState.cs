namespace TileMind.Common.Models;

/// <summary>
/// 全局对局状态。
/// </summary>
public class GameState
{
    public Dictionary<SeatPosition, PlayerState> Players { get; set; } = new();
    public List<MahjongAction> ActionLog { get; set; } = new();
    public long CurrentFrameNumber { get; set; }
    public int RoundWind { get; set; }
    public int Honba { get; set; }
    public int RemainingTiles { get; set; }
    public List<TrackedTile> DoraIndicators { get; set; } = new();
    public bool IsFirstFrame { get; set; } = true;
}
