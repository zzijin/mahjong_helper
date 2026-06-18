namespace TileMind.Common.Models;

/// <summary>
/// 帧状态事件中枢（Singleton）。
/// GamePipelineService 每帧处理后发布，UI 订阅。
/// </summary>
public class FrameStateHub
{
    /// <summary>Stage 1：静态分析完成（每帧都发，追踪/非追踪都发）。</summary>
    public event Action<AnalyzedFrame>? FrameAnalyzed;

    /// <summary>Stage 2：状态追踪动作（仅追踪模式）。</summary>
    public event Action<List<MahjongAction>>? ActionsDetected;

    /// <summary>帧耗时（每帧都发，Pipeline 元数据）。</summary>
    public event Action<FrameTimingInfo>? FrameTiming;

    /// <summary>牌型分析结果（每帧都发）。</summary>
    public event Action<TileAnalysisResult>? TileAnalysisReady;

    public void PublishAnalysis(AnalyzedFrame analysis)
    {
        FrameAnalyzed?.Invoke(analysis);
    }

    public void PublishActions(List<MahjongAction> actions)
    {
        ActionsDetected?.Invoke(actions);
    }

    public void PublishTiming(FrameTimingInfo timing)
    {
        FrameTiming?.Invoke(timing);
    }

    public void PublishTileAnalysis(TileAnalysisResult analysis)
    {
        TileAnalysisReady?.Invoke(analysis);
    }
}
