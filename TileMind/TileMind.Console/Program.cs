

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using SharpDX;
using System.Diagnostics;
using TileMind.Common.Config;
using TileMind.Common.Logging;
using TileMind.Vision.Detection;
using TileMind.Vision.ScreenCapture;

namespace TileMind.Console
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var imagePath = @".\testdatas\0250.png";

            var services = new ServiceCollection();
            ConfigureServices(services);
            var _serviceProvider = services.BuildServiceProvider();

            var yoloDetectorPool = _serviceProvider.GetRequiredService<YoloDetectorPoolService>();
            var screenCaptureService = _serviceProvider.GetRequiredService<IScreenCaptureService>();
            var frameFusionService = _serviceProvider.GetRequiredService<FrameFusionService>();

            var yoloDetector = yoloDetectorPool.Rent();

            //using var image = new Mat(imagePath);

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
                var fusionResult = frameFusionService.ProcessFrameFusion();

                stopwatch.Stop();
                System.Console.WriteLine($"Detection completed {i} in {stopwatch.ElapsedMilliseconds} ms");
            };

            System.Console.WriteLine("Press Enter to exit...");
            System.Console.ReadLine();
        }

        private static Dictionary<string,string> GetVisionConfig()
        {
            var classFilePath = @".\models\classes.txt";
            var classNames = File.ReadAllLines(classFilePath);
            var modelPath = @".\models\yolov8m-fp32.onnx";

            var visionConfig = new Dictionary<string, string>()
            {
                [nameof(YoloOptions.ModelPath)] = modelPath,
                [nameof(YoloOptions.ClassNames)] = string.Join(",", classNames),
                [nameof(YoloOptions.ConfidenceThreshold)] = "0.40",
                [nameof(YoloOptions.IouThreshold)] = "0.50",
                [nameof(YoloOptions.GpuDeviceId)] = "0",
                [nameof(YoloOptions.InputSize)] = "1280",
                [nameof(YoloOptions.MinDetectorPoolSize)] = "2",
                [nameof(YoloOptions.MaxDetectorPoolSize)] = "6",
                [nameof(YoloOptions.RentTimeoutSeconds)] = "5",

                [nameof(ScreenCaptureOptions.AdapterIndex)] = "0",
                [nameof(ScreenCaptureOptions.OutputIndex)] = "0",

                [nameof(FrameFusionOptions.EnableFusion)] = "true",
                [nameof(FrameFusionOptions.MaxFusionFrameCount)] = "5",
                [nameof(FrameFusionOptions.MovementThreshold)] = "0.05",
                [nameof(FrameFusionOptions.FusionConfidenceThreshold)] = "0.40",
                [nameof(FrameFusionOptions.FusionIouThreshold)] = "0.80",
            };

            return visionConfig;
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            //注册公共服务
            services.AddLogging(builder => builder.AddTileMindLogging());

            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddInMemoryCollection(GetVisionConfig()) // 添加视觉配置项
                .Build();
            services.AddSingleton<IConfiguration>(config);

            //注册视觉服务
            services.AddScoped<YoloDetectorPoolService>();
            services.AddScoped<IScreenCaptureService, DxgiScreenCaptureService>();
            services.AddScoped<FrameFusionService>();

            //注册AI服务
        }
    }
}
