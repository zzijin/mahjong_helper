namespace TileMind.Common.Models;

/// <summary>
/// 单帧静态分析结果：包含分离后的手牌、副露组及其类型判定。
/// 在追踪/非追踪模式下均可使用。
/// </summary>
public class AnalyzedFrame
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<SeatPosition, PlayerFrameAnalysis> Players { get; set; } = new();
    public Dictionary<SeatPosition, List<DetectionResult>> DiscardPondDetections { get; set; } = new();
    public List<DetectionResult> DoraIndicatorDetections { get; set; } = new();
    /// <summary>从指示牌推算出的实际宝牌（已去重）。</summary>
    public List<TileType> DoraTiles { get; set; } = new();

    /// <summary>当前帧的活跃玩家（手牌+副露总数 = 14 + 杠数）。null 表示无法判定。</summary>
    public SeatPosition? ActivePlayer { get; set; }
}

/// <summary>
/// 单个玩家在当前帧的静态分析结果。
/// </summary>
public class PlayerFrameAnalysis
{
    public SeatPosition Seat { get; set; }
    /// <summary>分离后的纯手牌（不含副露）。</summary>
    public List<DetectionResult> HandTiles { get; set; } = new();
    /// <summary>副露组及其类型判定。</summary>
    public List<MeldAnalysis> Melds { get; set; } = new();
    /// <summary>弃牌区是否检测到立直牌（横置牌）。</summary>
    public bool HasRiichiDiscard { get; set; }
    /// <summary>立直弃牌的具体检测框。</summary>
    public DetectionResult? RiichiDiscardTile { get; set; }
}

/// <summary>
/// 单个副露组的静态分析（类型 + 牌）。
/// </summary>
public class MeldAnalysis
{
    public MeldType MeldType { get; set; }
    public List<DetectionResult> Tiles { get; set; } = new();
}
