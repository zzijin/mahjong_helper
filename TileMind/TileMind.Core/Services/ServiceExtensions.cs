using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using TileMind.Common.Config;
using TileMind.Common.Logging;
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
                    .AddJsonFile(ScreenCaptureOptions.SettingFilePath, optional: true, reloadOnChange: true)
                    .AddJsonFile(FrameFusionOptions.SettingFilePath, optional: true, reloadOnChange: true)
                    .Build();

                services.AddOptions();
                services.AddSingleton<IConfiguration>(config);
                services.Configure<YoloOptions>(config.GetSection("Yolo"));
                services.Configure<ScreenCaptureOptions>(config.GetSection("ScreenCapture"));
                services.Configure<FrameFusionOptions>(config.GetSection("FrameFusion"));
                services.Configure<GameStateTrackerOptions>(config.GetSection("GameState"));
            }

            public void AddBaseServices()
            {
                //注册公共服务
                services.AddLogging(builder => builder.AddTileMindLogging());

                //注册视觉服务
                services.AddScoped<YoloDetectorPoolService>();
                services.AddScoped<IScreenCaptureService, DxgiScreenCaptureService>();
                services.AddScoped<FrameFusionService>();

                //注册游戏状态追踪服务
                services.AddSingleton<GameStateTracker>();
                services.AddSingleton<GameRecorderService>();

                //注册流水线服务（连接 Vision → Core）
                services.AddScoped<GamePipelineService>();

                //注册AI服务
            }
        }
    }
}
