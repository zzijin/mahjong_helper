using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TileMind.Common.Config;
using TileMind.Common.Helpers;
using TileMind.Common.Logging;
using TileMind.Common.Models;
using TileMind.Vision.Detection;
using TileMind.Vision.ScreenCapture;
using RectangleF = System.Drawing.RectangleF;

namespace TileMind.Core.Services
{
    public static class ServiceExtensions
    {

        extension(IServiceCollection services)
        {
            public void AddBaseConfig()
            {
                var config = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile(YoloOptions.SettingFilePath, optional: true, reloadOnChange: true)
                    .AddJsonFile(FrameFusionOptions.SettingFilePath, optional: true, reloadOnChange: true)
                    .AddJsonFile(GameStateTrackerOptions.SettingFilePath, optional: true, reloadOnChange: true)
                    .AddJsonFile(PipelineOptions.SettingFilePath, optional: true, reloadOnChange: true)
                    .AddJsonFile(OverlayOptions.SettingFilePath, optional: true, reloadOnChange: true)
                    .Build();

                services.AddSingleton<IConfiguration>(config);

                // 所有配置统一用 System.Text.Json 加载为 Singleton
                services.AddSingleton(SettingConfigExtensions.Load<YoloOptions>(
                    YoloOptions.SettingFilePath) ?? new YoloOptions());
                services.AddSingleton(SettingConfigExtensions.Load<FrameFusionOptions>(
                    FrameFusionOptions.SettingFilePath) ?? new FrameFusionOptions());
                services.AddSingleton(SettingConfigExtensions.Load<GameStateTrackerOptions>(
                    GameStateTrackerOptions.SettingFilePath) ?? new GameStateTrackerOptions());
                services.AddSingleton(SettingConfigExtensions.Load<PipelineOptions>(
                    PipelineOptions.SettingFilePath) ?? new PipelineOptions());
                services.AddSingleton(SettingConfigExtensions.Load<OverlayOptions>(
                    OverlayOptions.SettingFilePath) ?? new OverlayOptions());

                var screenOpts = SettingConfigExtensions.Load<ScreenCaptureOptions>(
                    ScreenCaptureOptions.SettingFilePath) ?? new ScreenCaptureOptions();

                // 先尝试用游戏窗口客户区解析，失败则用全屏 Fallback
                var clientRect = WindowFinderHelper.FindClientRect(screenOpts.GameProcessName ?? "");
                RectangleF refRect;
                if (clientRect.HasValue)
                    refRect = clientRect.Value;
                else
                    refRect = WindowFinderHelper.GetMonitorBounds(screenOpts.OutputIndex);

                screenOpts.ResolveAbsoluteCoordsFromRatios(refRect);
                services.AddSingleton(screenOpts);
            }

            public void AddBaseServices()
            {
                //注册公共服务
                services.AddLogging(builder => builder.AddTileMindLogging());

                //注册视觉服务
                services.AddScoped<YoloDetectorPoolService>();
                services.AddScoped<IScreenCaptureService, DxgiScreenCaptureService>();
                services.AddScoped<FrameFusionService>();

                //注册事件中枢（连接 Core → UI）
                services.AddSingleton<FrameStateHub>();

                //注册静态分析服务
                services.AddScoped<FrameAnalyzerService>();

                //注册牌型分析服务
                services.AddScoped<TileMind.Algorithm.TileAnalysisService>();

                //注册游戏状态追踪服务
                services.AddScoped<GameStateTracker>();
                services.AddScoped<GameRecorderService>();

                //注册流水线服务（连接 Vision → Core）
                services.AddScoped<GamePipelineService>();

                //注册AI服务
            }
        }
    }
}
