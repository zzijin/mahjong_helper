

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using SharpDX;
using System.Diagnostics;
using TileMind.Common.Config;
using TileMind.Common.Helpers;
using TileMind.Common.Logging;
using TileMind.Core.Services;
using TileMind.Vision.Detection;
using TileMind.Vision.ScreenCapture;

namespace TileMind.Console
{
    internal class Program
    {
        static void Main(string[] args)
        {

            var services = new ServiceCollection();
            ConfigureServices(services);
            var _serviceProvider = services.BuildServiceProvider();

            //YoloDetectorPoolService_Test(_serviceProvider);
            GameRecorderService_Test(_serviceProvider);


            System.Console.WriteLine("Press Enter to exit...");
            System.Console.ReadLine();
        }

        private static void YoloDetectorPoolService_Test(ServiceProvider serviceProvider)
        {
            var imagePath = @".\testdatas\0245.png";

            var yoloDetectorPool = serviceProvider.GetRequiredService<YoloDetectorPoolService>();
            var screenCaptureService = serviceProvider.GetRequiredService<IScreenCaptureService>();
            var frameFusionService = serviceProvider.GetRequiredService<FrameFusionService>();
            var gameRecorderService = serviceProvider.GetRequiredService<GameRecorderService>();

            var yoloDetector = yoloDetectorPool.Rent();

            using var image = new Mat(imagePath);

            for (int i = 0; i < 10; i++)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();

                //var image = screenCaptureService.CaptureFrame();
                //if( image == null ) 
                //{
                //    System.Console.WriteLine($"Failed to capture screen {i}.");
                //    continue;
                //}
                //var detections = yoloDetector.Detect(imagePath);
                //var detections = yoloDetector.Detect(image);

                //yoloDetector.DetectAndSave(@".\testdatas\0250.png", $@".\testdatas\0250_output_{i}.png");
                //yoloDetector.DetectAndSave(image, $@".\testdatas\capture_output_{i}.png");
                //var fusionResult = frameFusionService.ProcessFrameFusion();

                stopwatch.Stop();
                System.Console.WriteLine($"Detection completed {i} in {stopwatch.ElapsedMilliseconds} ms");
            }
            ;
        }

        private static void GameRecorderService_Test(ServiceProvider serviceProvider)
        {
            var yoloDetectorPool = serviceProvider.GetRequiredService<YoloDetectorPoolService>();
            var gamePipelineService = serviceProvider.GetRequiredService<GamePipelineService>();

            var yoloDetector = yoloDetectorPool.Rent();
            gamePipelineService.StartNewRound();
            for (int i = 0; i < 10; i++)
            {
                var imagePath = @$".\testdatas\{i:D4}.png";
                var detections = yoloDetector.Detect(imagePath);
                gamePipelineService.ProcessFrame();
            } 
        }

        private static Dictionary<string,string> GetVisionConfig()
        {
            var classFilePath = @".\models\classes.txt";
            var classNames = File.ReadAllLines(classFilePath);
            var modelPath = @".\models\yolov8m.onnx";

            var visionConfig = new Dictionary<string, string>()
            {
                [$"Yolo:{nameof(YoloOptions.ModelPath)}"] = modelPath,
                [$"Yolo:{nameof(YoloOptions.ClassNames)}"] = string.Join(",", classNames),
                [$"Yolo:{nameof(YoloOptions.ConfidenceThreshold)}"] = "0.40",
                [$"Yolo:{nameof(YoloOptions.IouThreshold)}"] = "0.50",
                [$"Yolo:{nameof(YoloOptions.GpuDeviceId)}"] = "0",
                [$"Yolo:{nameof(YoloOptions.InputSize)}"] = "1280",
                [$"Yolo:{nameof(YoloOptions.MinDetectorPoolSize)}"] = "2",
                [$"Yolo:{nameof(YoloOptions.MaxDetectorPoolSize)}"] = "6",
                [$"Yolo:{nameof(YoloOptions.RentTimeoutSeconds)}"] = "5",

                [$"ScreenCapture:{nameof(ScreenCaptureOptions.AdapterIndex)}"] = "0",
                [$"ScreenCapture:{nameof(ScreenCaptureOptions.OutputIndex)}"] = "0",

                [$"FrameFusion:{nameof(FrameFusionOptions.EnableFusion)}"] = "true",
                [$"FrameFusion:{nameof(FrameFusionOptions.MaxFusionFrameCount)}"] = "5",
                [$"FrameFusion:{nameof(FrameFusionOptions.MovementThreshold)}"] = "0.05",
                [$"FrameFusion:{nameof(FrameFusionOptions.FusionConfidenceThreshold)}"] = "0.40",
                [$"FrameFusion:{nameof(FrameFusionOptions.FusionIouThreshold)}"] = "0.80",
            };

            return visionConfig;
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddInMemoryCollection(GetVisionConfig()) // 添加视觉配置项
                .Build();

            services.AddOptions();
            services.AddSingleton<IConfiguration>(config);
            services.Configure<YoloOptions>(config.GetSection("Yolo"));
            services.Configure<ScreenCaptureOptions>(config.GetSection("ScreenCapture"));
            services.Configure<FrameFusionOptions>(config.GetSection("FrameFusion"));

            //注册公共服务
            services.AddBaseServices();
        }
    }
}
