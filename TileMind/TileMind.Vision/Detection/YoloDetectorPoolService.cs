using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using System.Collections.Concurrent;
using TileMind.Common.Config;
using TileMind.Vision.ScreenCapture;


namespace TileMind.Vision.Detection
{
    /// <summary>
    /// 基于对象池的 YOLOv8 检测器管理器，用于高性能多线程推理。
    /// </summary>
    public class YoloDetectorPoolService : IDisposable
    {
        private readonly IOptionsSnapshot<YoloOptions> _options;
        private readonly ILogger<YoloDetectorPoolService> _logger;
        private readonly ILogger<YoloDetector> _detectorLogger;

        private readonly ConcurrentBag<YoloDetector> _pool = new ConcurrentBag<YoloDetector>();
        private readonly string _modelPath;
        private readonly string[] _classNames;
        private readonly float _confidenceThreshold;
        private readonly float _iouThreshold;
        private readonly bool _useCuda;
        private readonly int _minPoolSize = 2; // 最小池大小，确保至少有几个实例可用
        private readonly int _maxPoolSize = 6; // 最大池大小，确保池不会无限增长
        private readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(5); // 获取实例的默认超时时间
        private readonly SemaphoreSlim _semaphore;
        //写入锁，确保在创建新实例时不会超过最大池大小
        private readonly object _writeLocker = new object();
        private bool _disposed;

        public YoloDetectorPoolService(IOptionsSnapshot<YoloOptions> options, ILogger<YoloDetectorPoolService> poolLogger, ILogger<YoloDetector> detectorLogger)
        {
            _options = options;
            _logger = poolLogger;
            _detectorLogger = detectorLogger;

            var opts = options.Value;
            _modelPath = opts.ModelPath;
            _classNames = opts.ClassNames;
            _confidenceThreshold = opts.ConfidenceThreshold;
            _iouThreshold = opts.IouThreshold;
            _useCuda = opts.GpuDeviceId >= 0;
            _minPoolSize = opts.MinDetectorPoolSize;
            _maxPoolSize = opts.MaxDetectorPoolSize;
            _defaultTimeout = TimeSpan.FromSeconds(opts.RentTimeoutSeconds);

            _semaphore = new SemaphoreSlim(_minPoolSize, _maxPoolSize);
            // 预热对象池，提前创建一定数量的检测器实例以减少首次使用时的延迟
            PreWarm(_minPoolSize);
        }

        /// <summary>
        /// 从池中获取一个 Yolov8Detector 实例。如果池为空，则创建一个新实例。
        /// </summary>
        public YoloDetector? Rent()
        {
            _semaphore.Wait();
            if (_pool.TryTake(out var detector))
            {
                return detector;
            }
            return null;
        }

        public async Task<YoloDetector?> RentAsync(TimeSpan? timeout = null)
        {
            var effectiveTimeout = timeout ?? _defaultTimeout;

            // 获取信号量，超时则返回空
            if (!await _semaphore.WaitAsync(effectiveTimeout).ConfigureAwait(false))
                return null;

            try
            {
                // 先尝试从池中取已有对象
                if (_pool.TryTake(out var detector))
                    return detector;

                try
                {
                    lock (_writeLocker)
                    {
                        // 创建新对象
                        var newDetector = new YoloDetector(_options, _detectorLogger);

                        //暂不使用，在未达最大池时，优先创建新对象
                        ////双重检查，避免在构造期间其他线程归还对象或创建新对象
                        //if (_pool.TryTake(out detector))
                        //{
                        //    newDetector.Dispose(); // 释放多余资源
                        //    return detector;
                        //}
                        //else
                        //    newDetector.Dispose();

                        _pool.Add(newDetector);
                        _semaphore.Release();

                        return newDetector;
                    }
                }
                catch
                {
                    throw;
                }
            }
            catch
            {
                // 构造或添加过程中发生异常，必须释放信号量
                _semaphore.Release();
                return null;
            }
        }

        /// <summary>
        /// 将 Yolov8Detector 实例归还到池中。
        /// </summary>
        public void Return(YoloDetector detector)
        {
            if (detector == null) return;
            _pool.Add(detector);
            _semaphore.Release();
        }

        /// <summary>
        /// 预先在池中创建指定数量的实例，以预热对象池。
        /// </summary>
        public void PreWarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                _pool.Add(new YoloDetector(_options, _detectorLogger));
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                while (_pool.TryTake(out var detector))
                {
                    detector.Dispose();
                }
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}