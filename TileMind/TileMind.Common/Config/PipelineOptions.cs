namespace TileMind.Common.Config;

/// <summary>
/// 流水线行为控制选项（暂不持久化到 JSON）。
/// </summary>
public class PipelineOptions
{
    /// <summary>是否启用对局状态追踪。关闭时仅输出每帧的识别快照。</summary>
    public bool EnableStateTracking { get; set; } = true;
}
