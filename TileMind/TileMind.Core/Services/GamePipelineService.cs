using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using TileMind.Common.Config;
using TileMind.Common.Models;
using TileMind.Vision.ScreenCapture;

namespace TileMind.Core.Services;

/// <summary>
/// 游戏流水线服务：连接 Vision 层的屏幕捕获/识别与 Core 层的静态分析/状态追踪。
/// 流程：帧融合 → 区域路由 → 静态分析 → [可选] 状态追踪 → UI 发布。
/// </summary>
public class GamePipelineService
{
    private readonly FrameFusionService _frameFusion;
    private readonly FrameAnalyzerService _analyzer;
    private readonly GameRecorderService _gameRecorder;
    private readonly ScreenCaptureOptions _screenOpts;
    private readonly PipelineOptions _pipelineOpts;
    private readonly FrameStateHub _frameStateHub;
    private readonly ILogger<GamePipelineService> _logger;

    public GamePipelineService(
        FrameFusionService frameFusion,
        FrameAnalyzerService analyzer,
        GameRecorderService gameRecorder,
        ScreenCaptureOptions screenOpts,
        PipelineOptions pipelineOpts,
        FrameStateHub frameStateHub,
        ILogger<GamePipelineService> logger)
    {
        _frameFusion = frameFusion;
        _analyzer = analyzer;
        _gameRecorder = gameRecorder;
        _screenOpts = screenOpts;
        _pipelineOpts = pipelineOpts;
        _frameStateHub = frameStateHub;
        _logger = logger;
    }

    public GameState CurrentGameState => _gameRecorder.CurrentState;

    public event Action<MahjongAction>? OnActionRecorded
    {
        add => _gameRecorder.OnActionRecorded += value;
        remove => _gameRecorder.OnActionRecorded -= value;
    }

    /// <summary>
    /// 执行一次完整的流水线步骤：采集 → 识别 → 融合 → 路由 → 静态分析 → [状态追踪] → UI。
    /// </summary>
    public List<MahjongAction> ProcessFrame()
    {
        var totalSw = Stopwatch.StartNew();
        var stepSw = Stopwatch.StartNew();

        List<DetectionResult> fullScreenDetections;
        try
        {
            fullScreenDetections = _frameFusion.ProcessFrameFusion();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FrameFusion 处理失败。");
            return new();
        }
        double fusionTotalMs = stepSw.Elapsed.TotalMilliseconds;
        var fusionTiming = _frameFusion.LastTiming;

        if (fullScreenDetections.Count == 0)
        {
            _logger.LogDebug("本帧无检测结果。");
            return new();
        }

        return ProcessFrame(fullScreenDetections, fusionTotalMs, totalSw);
    }


    public async Task<List<MahjongAction>> ProcessFrameFromLocalAsync(string imagePath)
    {
        List<DetectionResult> fullScreenDetections;
        try
        {
            fullScreenDetections = await _frameFusion.ProcessFrameFusionFromLocalAsync(imagePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FrameFusion 处理失败。");
            return new();
        }

        if (fullScreenDetections.Count == 0)
        {
            _logger.LogDebug("本帧无检测结果。");
            return new();
        }

        return ProcessFrame(fullScreenDetections);
    }

    public List<MahjongAction> ProcessFrame(List<DetectionResult> fullScreenDetections)
        => ProcessFrame(fullScreenDetections, 0, null);

    private List<MahjongAction> ProcessFrame(List<DetectionResult> fullScreenDetections, double fusionTotalMs, Stopwatch? totalSw)
    {
        if (fullScreenDetections.Count == 0)
        {
            _logger.LogDebug("本帧无检测结果。");
            return new();
        }

        var stepSw = Stopwatch.StartNew();
        var fusionTiming = _frameFusion.LastTiming;

        // 1. 区域路由 → FrameDetections
        var frameInput = RouteDetections(fullScreenDetections);
        double routingMs = stepSw.Elapsed.TotalMilliseconds;

        // 2. 静态分析 → AnalyzedFrame（两种模式都执行）
        stepSw.Restart();
        var analysis = _analyzer.Analyze(frameInput);
        double analysisMs = stepSw.Elapsed.TotalMilliseconds;

        // 3. Stage 1 UI 发布（每帧都发）
        _frameStateHub.PublishAnalysis(analysis);

        // 4. 状态追踪（可选）
        List<MahjongAction> actions;
        stepSw.Restart();
        if (_pipelineOpts.EnableStateTracking)
        {
            if (analysis.ActivePlayer == null)
            {
                _logger.LogDebug("静态分析无法判定活跃玩家，跳过本帧状态追踪。");
                actions = new();
            }
            else
            {
                actions = _gameRecorder.ProcessFrame(analysis);
                _frameStateHub.PublishActions(actions);
            }
        }
        else
        {
            actions = new();
        }
        double trackingMs = stepSw.Elapsed.TotalMilliseconds;

        double totalMs = totalSw?.Elapsed.TotalMilliseconds ?? fusionTotalMs + routingMs + analysisMs + trackingMs;
        _frameStateHub.PublishTiming(new FrameTimingInfo
        {
            CaptureMs = fusionTiming?.CaptureMs ?? 0,
            DetectMs = fusionTiming?.DetectMs ?? 0,
            FusionMs = fusionTiming?.FusionMs ?? fusionTotalMs,
            RoutingMs = routingMs,
            AnalysisMs = analysisMs,
            TrackingMs = trackingMs,
            TotalMs = totalMs
        });

        return actions;
    }

    /// <summary>
    /// 开始新的一局（重置状态追踪基线）。
    /// </summary>
    public void StartNewRound()
    {
        _gameRecorder.StartNewRound();
    }

    /// <summary>
    /// 将全屏检测结果按 ScreenCaptureOptions 中定义的区域四边形路由至各玩家/区域。
    /// </summary>
    private FrameDetections RouteDetections(List<DetectionResult> detections)
    {
        var frameInput = new FrameDetections { Timestamp = DateTime.UtcNow };

        foreach (SeatPosition seat in Enum.GetValues<SeatPosition>())
        {
            frameInput.HandAndMeldDetections[seat] = new List<DetectionResult>();
            frameInput.DiscardPondDetections[seat] = new List<DetectionResult>();
        }

        foreach (var det in detections)
        {
            var center = new Point(
                det.BoundingBox.X + det.BoundingBox.Width / 2,
                det.BoundingBox.Y + det.BoundingBox.Height / 2);

            if (TryRouteToRegion(det, center, _screenOpts.DoraIndicatorArea))
            {
                frameInput.DoraIndicatorDetections.Add(det);
                continue;
            }

            if (TryRouteToRegion(det, center, _screenOpts.SelfHandAndMeldArea))
            {
                frameInput.HandAndMeldDetections[SeatPosition.Self].Add(det);
                continue;
            }
            if (TryRouteToRegion(det, center, _screenOpts.RightHandAndMeldArea))
            {
                frameInput.HandAndMeldDetections[SeatPosition.Right].Add(det);
                continue;
            }
            if (TryRouteToRegion(det, center, _screenOpts.OppositeHandAndMeldArea))
            {
                frameInput.HandAndMeldDetections[SeatPosition.Opposite].Add(det);
                continue;
            }
            if (TryRouteToRegion(det, center, _screenOpts.LeftHandAndMeldArea))
            {
                frameInput.HandAndMeldDetections[SeatPosition.Left].Add(det);
                continue;
            }

            if (TryRouteToRegion(det, center, _screenOpts.SelfDiscardPondArea))
            {
                frameInput.DiscardPondDetections[SeatPosition.Self].Add(det);
                continue;
            }
            if (TryRouteToRegion(det, center, _screenOpts.RightDiscardPondArea))
            {
                frameInput.DiscardPondDetections[SeatPosition.Right].Add(det);
                continue;
            }
            if (TryRouteToRegion(det, center, _screenOpts.OppositeDiscardPondArea))
            {
                frameInput.DiscardPondDetections[SeatPosition.Opposite].Add(det);
                continue;
            }
            if (TryRouteToRegion(det, center, _screenOpts.LeftDiscardPondArea))
            {
                frameInput.DiscardPondDetections[SeatPosition.Left].Add(det);
                continue;
            }
        }

        return frameInput;
    }

    private static bool TryRouteToRegion(DetectionResult det, Point center, Point[] quad)
    {
        if (quad.Length != 4) return false;
        if (quad.All(p => p.X == 0 && p.Y == 0)) return false;
        return IsPointInQuadrilateral(center, quad);
    }

    internal static bool IsPointInQuadrilateral(Point p, Point[] quad)
    {
        Span<int> signs = stackalloc int[4];
        for (int i = 0; i < 4; i++)
        {
            var a = quad[i];
            var b = quad[(i + 1) % 4];
            long cross = (long)(b.X - a.X) * (p.Y - a.Y) - (long)(b.Y - a.Y) * (p.X - a.X);
            signs[i] = Math.Sign(cross);
        }

        return signs.ToArray().All(s => s >= 0) || signs.ToArray().All(s => s <= 0);
    }
}
