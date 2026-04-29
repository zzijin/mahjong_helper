namespace TileMind.Common.Models;

/// <summary>
/// 单帧检测输入：Vision 层按玩家/区域分割后的检测结果集合。
/// </summary>
public class FrameDetections
{
    /// <summary>
    /// 各玩家手牌+副露合并区域的检测结果。
    /// </summary>
    public Dictionary<SeatPosition, List<DetectionResult>> HandAndMeldDetections { get; set; } = new();

    /// <summary>
    /// 各玩家弃牌区域的检测结果。
    /// </summary>
    public Dictionary<SeatPosition, List<DetectionResult>> DiscardPondDetections { get; set; } = new();

    /// <summary>
    /// 宝牌指示区域的检测结果。
    /// </summary>
    public List<DetectionResult> DoraIndicatorDetections { get; set; } = new();

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
