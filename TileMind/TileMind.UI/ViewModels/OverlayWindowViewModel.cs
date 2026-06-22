using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TileMind.Common.Config;
using TileMind.Common.Models;
using TileMind.Core.Services;
using TileMind.UI.Overlay;
using TileMind.UI.Overlay.OverlayBase;
using TileMind.UI.Overlay.OverlayBase.DrawingCommand;

namespace TileMind.UI.ViewModels;

public partial class OverlayWindowViewModel : ViewModel
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ScreenCaptureOptions _screenOpts;
    private readonly OverlayOptions _overlayOpts;
    private readonly MahjongTileCommandGenerator _commandGenerator = new();
    private readonly ILogger<OverlayWindowViewModel> _logger;

    private CancellationTokenSource? _cts;
    private IServiceScope? _pipelineScope;
    private Task? _pipelineTask;

    /// <summary>流水线是否正在运行。</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartPipelineCommand))]
    private bool _isPipelineRunning;

    /// <summary>覆盖层绘制项集合（绑定到 XAML ItemsSource）。</summary>
    public ObservableCollection<DrawingInfo> OverlayItems { get; } = new();

    /// <summary>当前已添加的区域项（用于开关切换）。</summary>
    private readonly List<ScreenRegionDrawingInfo> _regionItems = new();

    /// <summary>覆盖层功能开关配置。</summary>
    public OverlayOptions OverlayOptions => _overlayOpts;

    public OverlayWindowViewModel(
        FrameStateHub hub,
        ScreenCaptureOptions screenOpts,
        OverlayOptions overlayOpts,
        IServiceProvider serviceProvider,
        ILogger<OverlayWindowViewModel> logger)
    {
        _screenOpts = screenOpts;
        _overlayOpts = overlayOpts;
        _serviceProvider = serviceProvider;
        _logger = logger;

        // 区域数据是静态的，初始化时绘制
        if (_overlayOpts.ShowScreenRegions)
            DrawScreenRegions();

        hub.FrameAnalyzed += OnFrameAnalyzed;
        hub.FrameTiming += OnFrameTiming;
        hub.TileAnalysisReady += OnTileAnalysisReady;
    }

    private bool CanStartPipeline() => !IsPipelineRunning;

    [RelayCommand(CanExecute = nameof(CanStartPipeline))]
    private void StartPipeline()
    {
        if (_pipelineTask != null) return;

        IsPipelineRunning = true;
        _cts = new CancellationTokenSource();
        _pipelineScope = _serviceProvider.CreateScope();
        var pipeline = _pipelineScope.ServiceProvider.GetRequiredService<GamePipelineService>();

        _pipelineTask = Task.Run(async () =>
        {
            _logger.LogInformation("流水线开始。");
            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    pipeline.ProcessFrame();
                    await Task.Delay(500, _cts.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "流水线异常。");
            }
            finally
            {
                _logger.LogInformation("流水线停止。");
            }
        }, _cts.Token);
    }

    [RelayCommand]
    private void StopPipeline()
    {
        _cts?.Cancel();
        _pipelineTask?.Wait(TimeSpan.FromSeconds(5));
        _pipelineTask = null;
        _pipelineScope?.Dispose();
        _pipelineScope = null;
        IsPipelineRunning = false;
    }

    private void OnFrameAnalyzed(AnalyzedFrame analysis)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (_overlayOpts.ShowDetectionBoxes)
                RefreshDetectionBoxes(analysis);
            else
                RemoveDetectionBoxes();
        });
    }

    private DrawingInfo? _fpsItem;

    private void OnFrameTiming(FrameTimingInfo t)
    {
        if (!_overlayOpts.ShowTimingStats) return;
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (_fpsItem != null) OverlayItems.Remove(_fpsItem);
            string text = $"截取:{t.CaptureMs:F0}ms 推理:{t.DetectMs:F0}ms 融合:{t.FusionMs:F0}ms 路由:{t.RoutingMs:F0}ms 分析:{t.AnalysisMs:F0}ms 追踪:{t.TrackingMs:F0}ms | 总:{t.TotalMs:F0}ms ({t.Fps:F0}fps)";
            var cmd = new TextCommand
            {
                Text = text,
                Position = new Point(16, 24),
                FontSize = 15,
                Foreground = new SolidColorBrush(Color.FromArgb(230, 180, 255, 180)),
                Background = new SolidColorBrush(Color.FromArgb(180, 20, 20, 20))
            };
            _fpsItem = new MahjongTileDrawingInfo(Array.Empty<DetectionResult>(), new List<IDrawingCommand> { cmd });
            OverlayItems.Add(_fpsItem);
        });
    }

    private DrawingInfo? _analysisItem;

    private void OnTileAnalysisReady(TileAnalysisResult r)
    {
        if (!_overlayOpts.ShowWinningAnalysis) return;
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (_analysisItem != null) OverlayItems.Remove(_analysisItem);

            var sb = new System.Text.StringBuilder();
            string header = r.Shanten switch
            {
                -1 => "和了!",
                0 => $"听牌! 等牌 {r.WinOptions.Count} 种",
                _ => $"向听数: {r.Shanten}"
            };
            sb.AppendLine(header);

            if (r.IsTenpai)
            {
                foreach (var w in r.WinOptions.OrderByDescending(w => w.Points))
                {
                    string tileName = w.WinTile.ToString();
                    string yakuStr = w.YakuNames.Count > 0
                        ? string.Join(" ", w.YakuNames.Take(4))
                        : "";
                    sb.AppendLine($"  {tileName} 剩{w.Remaining}张 {w.Han}翻{w.Fu}符 {w.Points}点  {yakuStr}");
                }
            }
            else if (r.DiscardOptions.Count > 0)
            {
                sb.AppendLine("--- 打牌推荐 ---");
                foreach (var d in r.DiscardOptions.Take(5))
                {
                    string tileName = d.DiscardTile.ToString();
                    sb.AppendLine($"  打{tileName} → 向听{d.ShantenAfter} 受入{d.UniqueUkeire}种{d.TotalUkeire}枚");
                }
            }

            var cmd = new TextCommand
            {
                Text = sb.ToString().TrimEnd(),
                Position = new Point(SystemParameters.WorkArea.Right - 16, 48),
                FontSize = 13,
                Alignment = TextAlignment.Right,
                Foreground = new SolidColorBrush(Color.FromArgb(230, 255, 220, 140)),
                Background = new SolidColorBrush(Color.FromArgb(180, 20, 20, 20))
            };
            _analysisItem = new MahjongTileDrawingInfo(Array.Empty<DetectionResult>(), new List<IDrawingCommand> { cmd });
            OverlayItems.Add(_analysisItem);
        });
    }

    /// <summary>用当前帧的识别结果替换所有检测框。</summary>
    private void RefreshDetectionBoxes(AnalyzedFrame analysis)
    {
        RemoveDetectionBoxes();

        // 手牌
        foreach (SeatPosition seat in Enum.GetValues<SeatPosition>())
        {
            if (analysis.Players.TryGetValue(seat, out var player) && player.HandTiles.Count > 0)
            {
                var commands = player.HandTiles
                    .SelectMany(d => _commandGenerator.GenerateCommands(d))
                    .ToList();
                OverlayItems.Add(new PlayerTileDrawingInfo(seat, player.HandTiles, commands));
            }
        }

        // 副露
        foreach (SeatPosition seat in Enum.GetValues<SeatPosition>())
        {
            if (analysis.Players.TryGetValue(seat, out var player) && player.Melds.Count > 0)
            {
                foreach (var meld in player.Melds)
                {
                    var commands = meld.Tiles
                        .SelectMany(d => _commandGenerator.GenerateCommands(d, meldType: meld.MeldType))
                        .ToList();
                    OverlayItems.Add(new PlayerTileDrawingInfo(seat, meld.Tiles, commands));
                }
            }
        }

        // 弃牌
        foreach (SeatPosition seat in Enum.GetValues<SeatPosition>())
        {
            if (analysis.DiscardPondDetections.TryGetValue(seat, out var pondDets) && pondDets.Count > 0)
            {
                var commands = pondDets
                    .SelectMany(d => _commandGenerator.GenerateCommands(d))
                    .ToList();
                OverlayItems.Add(new PlayerTileDrawingInfo(seat, pondDets, commands));
            }
        }

        // 宝牌指示牌
        if (analysis.DoraIndicatorDetections.Count > 0)
        {
            var commands = analysis.DoraIndicatorDetections
                .SelectMany(d => _commandGenerator.GenerateCommands(d))
                .ToList();
            OverlayItems.Add(new MahjongTileDrawingInfo(analysis.DoraIndicatorDetections, commands));
        }

    }

    /// <summary>移除所有检测框项（保留区域项）。</summary>
    private void RemoveDetectionBoxes()
    {
        var toRemove = OverlayItems
            .Where(i => i is not ScreenRegionDrawingInfo && i != _fpsItem && i != _analysisItem)
            .ToList();
        foreach (var item in toRemove)
            OverlayItems.Remove(item);
    }

    // ─────────────── 区域绘制（静态数据，非帧级） ───────────────

    private void DrawScreenRegions()
    {
        if (_regionItems.Count > 0) return; // 已绘制

        AddRegionQuad("Self Hand+Meld", _screenOpts.SelfHandAndMeldArea, Colors.LimeGreen);
        AddRegionQuad("Right Hand+Meld", _screenOpts.RightHandAndMeldArea, Colors.DodgerBlue);
        AddRegionQuad("Opposite Hand+Meld", _screenOpts.OppositeHandAndMeldArea, Colors.OrangeRed);
        AddRegionQuad("Left Hand+Meld", _screenOpts.LeftHandAndMeldArea, Colors.Gold);

        AddRegionQuad("Self Discard", _screenOpts.SelfDiscardPondArea, Color.FromRgb(100, 200, 100));
        AddRegionQuad("Right Discard", _screenOpts.RightDiscardPondArea, Color.FromRgb(100, 150, 220));
        AddRegionQuad("Opposite Discard", _screenOpts.OppositeDiscardPondArea, Color.FromRgb(220, 130, 110));
        AddRegionQuad("Left Discard", _screenOpts.LeftDiscardPondArea, Color.FromRgb(200, 180, 80));

        AddRegionQuad("Dora Indicator", _screenOpts.DoraIndicatorArea, Colors.Magenta);
        AddRegionQuad("Info", _screenOpts.InfoArea, Colors.Cyan);
    }

    private void AddRegionQuad(string name, OpenCvSharp.Point[] quad, Color color)
    {
        if (quad.Length != 4) return;
        if (quad.All(p => p.X == 0 && p.Y == 0)) return;

        var points = new PointCollection();
        for (int i = 0; i < 4; i++)
            points.Add(new Point(quad[i].X, quad[i].Y));

        var commands = new List<IDrawingCommand>
        {
            new PolygonCommand
            {
                Points = points,
                IsClosed = true,
                IsFilled = true
            },
            new TextCommand
            {
                Text = name,
                Position = new Point(
                    (points[0].X + points[1].X + points[2].X + points[3].X) / 4,
                    (points[0].Y + points[1].Y + points[2].Y + points[3].Y) / 4),
                FontSize = 16,
                Alignment = TextAlignment.Center,
                Foreground = new SolidColorBrush(color),
                Background = new SolidColorBrush(Colors.Transparent)
            }
        };

        var item = new ScreenRegionDrawingInfo(name, commands, color);
        _regionItems.Add(item);
        OverlayItems.Add(item);
    }

    private void RemoveScreenRegions()
    {
        foreach (var item in _regionItems)
            OverlayItems.Remove(item);
        _regionItems.Clear();
    }
}
