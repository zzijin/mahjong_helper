using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TileMind.Common.Config;
using TileMind.Common.Helpers;
using TileMind.Common.Logging;
using TileMind.Common.Models;
using TileMind.Vision.Detection;
using TileMind.Vision.ScreenCapture;

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
                    //.AddJsonFile("appsettings.json")
                    // 视觉配置文件，当配置发生变化时自动重新加载配置
                    .AddJsonFile(YoloOptions.SettingFilePath, optional: true, reloadOnChange: true)
                    .AddJsonFile(FrameFusionOptions.SettingFilePath, optional: true, reloadOnChange: true)
                    .Build();

                services.AddOptions();
                services.AddSingleton<IConfiguration>(config);
                services.AddSingleton(new PipelineOptions());
                services.Configure<YoloOptions>(config.GetSection(YoloOptions.SectionName));
                services.Configure<FrameFusionOptions>(config.GetSection(FrameFusionOptions.SectionName));
                services.Configure<GameStateTrackerOptions>(config.GetSection(GameStateTrackerOptions.SectionName));

                // ScreenCaptureOptions 含 OpenCvSharp.Point[]，MS Config 无法绑定，改用 System.Text.Json 直接加载
                var screenOpts = SettingConfigExtensions.Load<ScreenCaptureOptions>(
                    ScreenCaptureOptions.SettingFilePath) ?? new ScreenCaptureOptions();
                screenOpts.ComputeDerivedAreas();
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
