namespace TileMind.Common.Models;

/// <summary>
/// FrameFusionService 内部各环节耗时（毫秒）。
/// </summary>
public class FrameFusionTiming
{
    public double CaptureMs { get; set; }
    public double DetectMs { get; set; }
    public double FusionMs { get; set; }
    public double TotalMs { get; set; }
}
