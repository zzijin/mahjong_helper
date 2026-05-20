using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenCvSharp;
using System.Collections.Concurrent;
using System.Drawing;
using TileMind.Common.Config;
using TileMind.Common.Models;
using TileMind.Vision.Detection;

namespace TileMind.Vision.ScreenCapture
{
    /// <summary>
    /// 多帧融合识别服务。
    /// </summary>
    public class FrameFusionService
    {
        private readonly YoloDetectorPoolService _detectorPool;
        private readonly IScreenCaptureService _captureService;
        private readonly ILogger<FrameFusionService> _logger;

        // 融合参数
        private readonly int _fusionFrameCount;       // 参与融合的帧数量，例如 3 帧
        private readonly float _movementThreshold;    // 帧间变化阈值，超过此值认为场景发生变化
        private readonly float _confidenceThreshold;  // 检测置信度阈值
        private readonly float _iouThreshold;         // IoU 阈值

        // 滑动窗口缓存，用于存储最近几帧的融合结果
        private readonly ConcurrentQueue<FusionFrameData> _frameCache = new ConcurrentQueue<FusionFrameData>();

        public FrameFusionService(
            YoloDetectorPoolService detectorPool,
            IScreenCaptureService captureService, IOptionsSnapshot<FrameFusionOptions> options, ILogger<FrameFusionService> logger)
        {
            _detectorPool = detectorPool;
            _captureService = captureService;
            _logger = logger;

            var opts = options.Value;
            _fusionFrameCount = opts.MaxFusionFrameCount;
            _movementThreshold = opts.MovementThreshold;
            _confidenceThreshold = opts.FusionConfidenceThreshold;
            _iouThreshold = opts.FusionIouThreshold;
        }

        public async Task<List<DetectionResult>> ProcessFrameFusionFromLocalAsync(string imagePath)
        {
            var fusionResult = await Task.Run(async () =>
            {
                // 从对象池获取一个检测器实例
                var detector = await _detectorPool.RentAsync();
                if (detector == null)
                {
                    _logger.LogError("无法从对象池获取检测器实例，跳过当前帧的检测。");
                    return [];
                }
                try
                {
                    // 将 Bitmap 转换为 Mat 以供检测器使用
                    // 执行检测
                    var result = detector.Detect(imagePath);

                    return result;
                }
                finally
                {
                    // 使用完毕后归还检测器到对象池
                    _detectorPool.Return(detector);
                }
            });

            _frameCache.Enqueue(new FusionFrameData
            {
                Timestamp = DateTime.UtcNow,
                FusionResult = fusionResult
            });

            while (_frameCache.Count > _fusionFrameCount)
            {
                _frameCache.TryDequeue(out _);
            }

            return fusionResult;
        }

        /// <summary>
        /// 执行一次多帧融合识别。
        /// </summary>
        /// <returns>融合后的最终识别结果。</returns>
        public List<DetectionResult> ProcessFrameFusion()
        {
            // 1. 快速连续采集多帧图像
            var frames = CaptureMultipleFrames(_fusionFrameCount);

            if(frames.Count == 0)
            {
                _logger.LogError("未能采集到任何帧，跳过融合处理。");
                return new List<DetectionResult>();
            }

            // 2. 检测场景是否发生显著变化，若无变化则返回上次结果，节省计算
            if (frames.Count >= 2 && !HasSceneChanged(frames[0], frames[1]))
            {
                // 如果缓存中有结果，直接返回最新的融合结果，避免重复推理
                if (_frameCache.TryPeek(out var lastResult) && lastResult.FusionResult != null)
                    return lastResult.FusionResult;
            }

            // 3. 使用对象池中的检测器并行推理每一帧
            var detectionTasks = new List<Task<List<DetectionResult>>>();
            foreach (var frame in frames)
            {
                var mat = frame;
                detectionTasks.Add(Task.Run(async () =>
                {
                    // 从对象池获取一个检测器实例
                    var detector = await _detectorPool.RentAsync();
                    if(detector == null)
                    {
                        _logger.LogError("无法从对象池获取检测器实例，跳过当前帧的检测。");
                        return [];
                    }
                    try
                    {
                        // 将 Bitmap 转换为 Mat 以供检测器使用
                        // 执行检测
                        var result = detector.Detect(mat);

                        mat.Dispose();
                        return result;
                    }
                    finally
                    {
                        // 使用完毕后归还检测器到对象池
                        _detectorPool.Return(detector);
                    }
                }));
            }
            var allFrameResults = Task.WhenAll(detectionTasks).Result;

            // 4. 对多帧检测结果进行融合（加权投票）
            var fusionResult = FuseResults(allFrameResults);

            // 5. 将融合结果加入滑动窗口缓存，以备下次使用
            _frameCache.Enqueue(new FusionFrameData
            {
                Timestamp = DateTime.UtcNow,
                FusionResult = fusionResult
            });

            // 6. 保持缓存大小不超过设定的融合帧数
            while (_frameCache.Count > _fusionFrameCount)
            {
                _frameCache.TryDequeue(out _);
            }

            return fusionResult;
        }

        /// <summary>
        /// 连续采集多帧图像。
        /// </summary>
        private List<Mat> CaptureMultipleFrames(int frameCount)
        {
            var frames = new List<Mat>();
            for (int i = 0; i < frameCount; i++)
            {
                var frame = _captureService.CaptureFrame();
                if (frame != null)
                    frames.Add(frame);
                else
                    break;
            }
            return frames;
        }

        /// <summary>
        /// 通过比较两帧图像的差异来判断场景是否发生变化。
        /// </summary>
        private bool HasSceneChanged(Mat frame1, Mat frame2)
        {
            using (var diff = new Mat())
            {
                // 计算两帧的绝对差
                Cv2.Absdiff(frame1, frame2, diff);
                // 转换为灰度图并计算平均值，得到差异度
                Cv2.CvtColor(diff, diff, ColorConversionCodes.BGR2GRAY);
                var meanDiff = diff.Mean().Val0 / 255.0;
                return meanDiff > _movementThreshold;
            }
        }

        /// <summary>
        /// 对多帧检测结果进行融合（加权投票策略）。
        /// 核心思想：最近一帧权重最高，并过滤掉在多帧中都不稳定的检测结果。
        /// </summary>
        private List<DetectionResult> FuseResults(List<DetectionResult>[] frameResults)
        {
            var allDetections = new List<(DetectionResult detection, int frameIndex, float weight)>();

            // 为每一帧的检测结果分配权重，最近一帧权重最高 (frameIndex 越大越新)
            for (int i = 0; i < frameResults.Length; i++)
            {
                // 简单的线性权重：越新的帧权重越高
                float weight = (float)(i + 1) / frameResults.Length;
                foreach (var detection in frameResults[i])
                {
                    if (detection.Confidence >= _confidenceThreshold)
                        allDetections.Add((detection, i, weight));
                }
            }

            if (!allDetections.Any())
                return new List<DetectionResult>();

            // 对检测框进行分组（使用简单的 IoU 匹配）
            var groups = GroupDetectionsByIoU(allDetections);

            // 为每个组生成一个融合结果
            var fusionResults = new List<DetectionResult>();
            foreach (var group in groups)
            {
                // 计算加权平均置信度
                float avgConfidence = group.Average(d => d.detection.Confidence * d.weight);

                // 如果加权平均置信度低于阈值，则丢弃这个检测组
                if (avgConfidence < _confidenceThreshold)
                    continue;

                // 选择置信度最高的检测结果作为代表，但用加权平均更新其置信度
                var bestDetection = group.OrderByDescending(d => d.detection.Confidence).First().detection;
                bestDetection.Confidence = avgConfidence;

                // 也可以考虑对边界框进行加权平均，这里简化处理
                fusionResults.Add(bestDetection);
            }

            return fusionResults;
        }

        /// <summary>
        /// 基于 IoU 将检测框分组。
        /// </summary>
        private List<List<(DetectionResult detection, int frameIndex, float weight)>> GroupDetectionsByIoU(
            List<(DetectionResult detection, int frameIndex, float weight)> detections)
        {
            var groups = new List<List<(DetectionResult detection, int frameIndex, float weight)>>();
            var used = new bool[detections.Count];

            for (int i = 0; i < detections.Count; i++)
            {
                if (used[i]) continue;
                var group = new List<(DetectionResult detection, int frameIndex, float weight)> { detections[i] };
                used[i] = true;

                for (int j = i + 1; j < detections.Count; j++)
                {
                    if (used[j]) continue;
                    var iou = CalculateIoU(detections[i].detection.BoundingBox, detections[j].detection.BoundingBox);
                    if (iou >= _iouThreshold)
                    {
                        group.Add(detections[j]);
                        used[j] = true;
                    }
                }
                groups.Add(group);
            }
            return groups;
        }

        /// <summary>
        /// 计算两个 OpenCvSharp Rect 的 IoU。
        /// </summary>
        private float CalculateIoU(OpenCvSharp.Rect a, OpenCvSharp.Rect b)
        {
            var intersection = OpenCvSharp.Rect.Intersect(a, b);
            if (intersection.Width <= 0 || intersection.Height <= 0)
                return 0;

            float intersectionArea = intersection.Width * intersection.Height;
            float unionArea = (a.Width * a.Height) + (b.Width * b.Height) - intersectionArea;
            return intersectionArea / unionArea;
        }

        /// <summary>
        /// 融合帧的数据结构。
        /// </summary>
        private class FusionFrameData
        {
            public DateTime Timestamp { get; set; }
            public List<DetectionResult> FusionResult { get; set; }
        }
    }
}