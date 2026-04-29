namespace TileMind.Common.Config;

/// <summary>
/// 游戏状态追踪的各项阈值配置。
/// </summary>
public class GameStateTrackerOptions
{
    public const string SettingFilePath = @".\settings\gamestatetrackersettings.json";

    /// <summary>帧间 tile 匹配的 IoU 阈值。</summary>
    public float MatchIouThreshold { get; set; } = 0.45f;

    /// <summary>连续未匹配帧数超过此值时 tile 视为移除。</summary>
    public int MaxConsecutiveMisses { get; set; } = 3;

    /// <summary>手牌行内 gap 与平均间距的比值，超过此值的 gap 视为手牌-副露分界。</summary>
    public float HandMeldGapMultiplier { get; set; } = 2.0f;

    /// <summary>副轴聚类容差（像素），用于区分不同的行/列。</summary>
    public float RowTolerance { get; set; } = 20f;

    /// <summary>主轴聚类容差（像素），副露候选 tile 在此距离内归为一组。</summary>
    public float MeldProximityThreshold { get; set; } = 10f;
}
