using Microsoft.Extensions.Logging;
using OpenCvSharp;
using TileMind.Common.Config;
using TileMind.Common.Models;
using TileMind.Vision.ScreenCapture;

namespace TileMind.Core.Services;

/// <summary>
/// 游戏流水线服务：连接 Vision 层的屏幕捕获/识别与 Core 层的对局状态追踪。
/// 调用 FrameFusionService 获取全屏识别结果，按 ScreenCaptureOptions 定义的
/// 各玩家区域四边形将检测结果路由至对应玩家/区域，最终传入 GameRecorderService。
/// </summary>
public class GamePipelineService
{
    private readonly FrameFusionService _frameFusion;
    private readonly GameRecorderService _gameRecorder;
    private readonly ScreenCaptureOptions _screenOpts;
    private readonly FrameStateHub _frameStateHub;
    private readonly ILogger<GamePipelineService> _logger;

    public GamePipelineService(
        FrameFusionService frameFusion,
        GameRecorderService gameRecorder,
        ScreenCaptureOptions screenOpts,
        FrameStateHub frameStateHub,
        ILogger<GamePipelineService> logger)
    {
        _frameFusion = frameFusion;
        _gameRecorder = gameRecorder;
        _screenOpts = screenOpts;
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
    /// 执行一次完整的流水线步骤：采集 → 识别 → 融合 → 路由 → 状态追踪。
    /// </summary>
    /// <returns>本帧检测到的游戏动作列表。</returns>
    public List<MahjongAction> ProcessFrame()
    {
        // 1. 调用 Vision 层获取融合识别结果
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

        if (fullScreenDetections.Count == 0)
        {
            _logger.LogDebug("本帧无检测结果。");
            return new();
        }

        return ProcessFrame(fullScreenDetections);
    }


    public async Task<List<MahjongAction>> ProcessFrameFromLocalAsync(string imagePath)
    {

        // 1. 调用 Vision 层获取融合识别结果
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
    {
        if (fullScreenDetections.Count == 0)
        {
            _logger.LogDebug("本帧无检测结果。");
            return new();
        }

        // 1. 按区域路由检测结果
        var frameInput = RouteDetections(fullScreenDetections);
        // 2. 传入对局记录服务
        var actions = _gameRecorder.ProcessFrame(frameInput);
        // 3. 通知 UI 覆盖层
        _frameStateHub.Publish(frameInput, actions);
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

        // 初始化各玩家区域的 bucket
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

            // 按优先级检查各区域（宝牌区优先，因为面积小、位置特殊）
            if (TryRouteToRegion(det, center, _screenOpts.DoraIndicatorArea))
            {
                frameInput.DoraIndicatorDetections.Add(det);
                continue;
            }

            // 检查四个玩家的手牌+副露合并区域
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

            // 检查四个玩家的弃牌区域
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

            // 不属于任何已知区域 → 忽略
        }

        return frameInput;
    }

    /// <summary>
    /// 判断检测结果中心点是否在给定四边形区域内。若区域未配置（全零）则返回 false。
    /// </summary>
    private static bool TryRouteToRegion(DetectionResult det, Point center, Point[] quad)
    {
        if (quad.Length != 4) return false;

        // 检查区域是否已配置（全零表示未配置）
        if (quad.All(p => p.X == 0 && p.Y == 0)) return false;

        return IsPointInQuadrilateral(center, quad);
    }

    /// <summary>
    /// 点是否在凸四边形内（cross product 法，兼容顺/逆时针顶点顺序）。
    /// </summary>
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
