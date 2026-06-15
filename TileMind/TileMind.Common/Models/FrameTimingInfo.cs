namespace TileMind.Common.Models;

/// <summary>
/// 单帧各处理环节的耗时统计（毫秒）。由 GamePipelineService 产出，UI 展示。
/// </summary>
public class FrameTimingInfo
{
    /// <summary>屏幕截取耗时。</summary>
    public double CaptureMs { get; set; }
    /// <summary>YOLO 推理耗时。</summary>
    public double DetectMs { get; set; }
    /// <summary>多帧融合耗时。</summary>
    public double FusionMs { get; set; }
    public double RoutingMs { get; set; }
    public double AnalysisMs { get; set; }
    public double TrackingMs { get; set; }
    public double TotalMs { get; set; }
    public double Fps => TotalMs > 0 ? 1000.0 / TotalMs : 0;
}
