using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TileMind.Common.Config;
using TileMind.Common.Models;
using TileMind.Vision.Tools;
using static System.Collections.Specialized.BitVector32;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TileMind.Vision.Detection
{
    /// <summary>
    /// 高性能 YOLOv8 目标检测器，基于 Microsoft.ML.OnnxRuntime。
    /// </summary>
    public class YoloDetector : IDisposable
    {
        private readonly ILogger _logger;

        private readonly InferenceSession _session;
        private readonly float _confidenceThreshold;
        private readonly float _iouThreshold;
        private readonly string _inputName;
        private readonly string _outputName;
        private readonly int _inputWidth;
        private readonly int _inputHeight;
        private readonly Type _inputType;
        private readonly int _outputWidth;
        private readonly int _outputHeight;
        private readonly Type _outputType;
        private readonly string[] _classNames;

        private bool _disposed = false;

        /// <summary>
        /// 初始化 YOLOv8 检测器。
        /// </summary>
        /// <param name="modelPath">ONNX 模型文件路径。</param>
        /// <param name="classNames">类别名称数组，顺序需与模型训练时一致。</param>
        /// <param name="confidenceThreshold">置信度阈值，低于此值的检测框将被忽略。</param>
        /// <param name="iouThreshold">交并比(IoU)阈值，用于非极大值抑制(NMS)。</param>
        /// <param name="useCuda">是否尝试使用 CUDA GPU 加速。</param>
        public YoloDetector(IOptionsSnapshot<YoloOptions> options, ILogger<YoloDetector> logger)
        {
            _logger = logger;
            var opts = options.Value;

            _classNames = opts.ClassNames;
            _confidenceThreshold = opts.ConfidenceThreshold;
            _iouThreshold = opts.IouThreshold;

            // 配置推理会话选项
            var sessionOptions = new SessionOptions();
            if (opts.GpuDeviceId >= 0)
            {
                try
                {
                    sessionOptions.AppendExecutionProvider_CUDA(opts.GpuDeviceId);
                }
                catch (DllNotFoundException ex)
                {
                    _logger.LogInformation($"CUDA provider load failed: {ex.Message}. 回退到 CPU。");
                    sessionOptions.AppendExecutionProvider_CPU();
                }
                catch (OnnxRuntimeException ex)
                {
                    _logger.LogInformation($"ONNX GPU 初始化失败: {ex.Message}. 回退到 CPU。");
                    sessionOptions.AppendExecutionProvider_CPU();
                }
            }
            else
            {
                sessionOptions.AppendExecutionProvider_CPU();
            }

            // 加载 ONNX 模型，创建推理会话
            _session = new InferenceSession(opts.ModelPath, sessionOptions);

            // 获取模型元数据
            // YOLOv8 模型通常只有一个名为 "images" 的输入
            _inputName = _session.InputMetadata.Keys.First();
            var inputInfo = _session.InputMetadata[_inputName];
            var inputShape = inputInfo.Dimensions;
            _inputHeight = inputShape[2];
            _inputWidth = inputShape[3];
            _inputType = inputInfo.ElementType;
            //inputShape[0] 输入批次， -1：动态批次，可一次输入任意数量图片
            //inputShape[1] 颜色通道数，RGB 通常为3
            //inputShape[2] 图像高度，-1：动态形状
            //inputShape[3] 图像宽度，-1：动态形状

            if (_inputHeight == -1 || _inputWidth == -1)
            {
                _logger.LogInformation($"模型支持动态输入尺寸，图像预处理将使用自适应模式。");
            }
            else if (_inputHeight != opts.InputSize || _inputWidth != opts.InputSize)
            {
                // 模型输入尺寸与默认值不同，预处理会自动适配
                _logger.LogInformation($"模型输入尺寸为 {_inputHeight}x{_inputWidth}，与配置的 {opts.InputSize}x{opts.InputSize} 不同，图像预处理将自动使用模型输入尺寸。");
            }

            _outputName = _session.OutputMetadata.Keys.First();
            var outputInfo = _session.OutputMetadata[_outputName];
            var outputShape = outputInfo.Dimensions;
            // 使用输入尺寸
            _outputHeight = _inputHeight;
            _outputWidth = _inputWidth;
            _outputType = outputInfo.ElementType;
        }

        public void DetectAndSave(string imagePath, string outputPath)
        {
            using var image = new Mat(imagePath);
            var detections = Detect(image);
            SaveDetections(image, detections, outputPath);
        }

        public void DetectAndSave(Mat image, string outputPath)
        {
            var detections = Detect(image);
            SaveDetections(image, detections, outputPath);
        }

        /// <summary>
        /// 对单张图片进行目标检测。
        /// </summary>
        /// <param name="imagePath">图片文件路径。</param>
        /// <returns>检测结果列表。</returns>
        public List<DetectionResult> Detect(string imagePath)
        {
            using var image = new Mat(imagePath);
            return Detect(image);
        }

        //使用半精度模型时的性能还需优化
        public List<DetectionResult> Detect(Mat image)
        {
            if (image.Empty())
                _logger.LogWarning("输入图像为空，无法进行检测。");

            // 记录原始图像尺寸，用于将预测坐标缩放回原图
            var originalWidth = image.Width;
            var originalHeight = image.Height;

            // --- 图像预处理 ---
            List<NamedOnnxValue> inputs;
            switch (_inputType)
            {
                case Type t when t == typeof(float):
                    {
                        inputs = PreprocessImage<float>(image);
                        break;
                    }
                //当前模型返回值为Microsoft.ML.OnnxRuntime.Float16，暂不支持Half类型输入
                case Type t when t == typeof(Half):
                    {
                        inputs = PreprocessImage<Half>(image);
                        break;
                    }
                case Type t when t == typeof(Microsoft.ML.OnnxRuntime.Float16):
                    {
                        inputs = this.PreprocessImageFloat(image, _inputName, _inputHeight, _inputWidth);
                        break;
                    }
                default:
                    {
                        _logger.LogError("输入图像为空，无法进行检测。");
                        throw new NotSupportedException($"不支持的Yolo模型输入类型: {_inputType}");
                    }
            }

            // --- 执行推理 ---
            using var results = _session.Run(inputs);

            // --- 后处理 (解析输出 + NMS) ---
            switch (_outputType)
            {
                case Type t when t == typeof(float):
                    {
                        var outputTensor = results.First(x => x.Name == _outputName).AsTensor<float>();
                        var detections = PostprocessOutput<float>(outputTensor, originalWidth, originalHeight);
                        return detections;
                    }
                //当前模型返回值为Microsoft.ML.OnnxRuntime.Float16，暂不支持Half类型输入
                case Type t when t == typeof(Half):
                    {
                        var outputTensor = results.First(x => x.Name == _outputName).AsTensor<Half>();
                        var detections = PostprocessOutput<Half>(outputTensor, originalWidth, originalHeight);
                        return detections;
                    }
                case Type t when t == typeof(Microsoft.ML.OnnxRuntime.Float16):
                    {
                        var outputTensor = results.First(x => x.Name == _outputName).AsTensor<Microsoft.ML.OnnxRuntime.Float16>();
                        var detections = this.PostprocessOutputFloat16(outputTensor, originalWidth, originalHeight, _classNames, _confidenceThreshold, _outputWidth, _outputHeight, ApplyNms);
                        return detections;
                    }
                default:
                    {
                        _logger.LogError("输入图像为空，无法进行检测。");
                        throw new NotSupportedException($"不支持的Yolo模型输出类型: {_outputType}");
                    }
            }
        }

        /// <summary>
        /// 图像预处理：调整大小、归一化、转换为张量。
        /// </summary>
        private List<NamedOnnxValue> PreprocessImage<T>(Mat image) where T : unmanaged, INumber<T>, IMinMaxValue<T>
        {
            Mat letterboxedImage;
            if (_inputHeight != -1 && _inputWidth != -1)
            {
                // 创建一个 letterbox 区域，保持宽高比，并用灰色填充
                letterboxedImage = new Mat(_inputHeight, _inputWidth, MatType.CV_8UC3, Scalar.Gray);
                var scale = Math.Min((float)_inputWidth / image.Width, (float)_inputHeight / image.Height);
                var scaledWidth = (int)(image.Width * scale);
                var scaledHeight = (int)(image.Height * scale);
                var offsetX = (_inputWidth - scaledWidth) / 2;
                var offsetY = (_inputHeight - scaledHeight) / 2;

                //调整图像大小并填充letterbox区域
                using var resizedImage = new Mat();
                Cv2.Resize(image, resizedImage, new Size(scaledWidth, scaledHeight));
                resizedImage.CopyTo(new Mat(letterboxedImage, new Rect(offsetX, offsetY, scaledWidth, scaledHeight)));
            }
            else
            {
                letterboxedImage = image;
            }

            // BGR (OpenCV 默认) 转 RGB，并转换为 指定输入类型
            using var rgbImage = new Mat();
            Cv2.CvtColor(letterboxedImage, rgbImage, ColorConversionCodes.BGR2RGB);

            rgbImage.ConvertTo(rgbImage, MatType.CV_32FC3, 1.0 / 255.0); // 归一化到 [0, 1]

            // 创建输入张量 (1, 3, Height, Width)
            var inputTensor = new DenseTensor<T>(new[] { 1, 3, _inputHeight, _inputWidth });
            var tensorSpan = inputTensor.Buffer.Span;

            // 计算每个通道在 Span 中的起始偏移量
            int channelStride = _inputHeight * _inputWidth;
            int offsetR = 0 * channelStride;
            int offsetG = 1 * channelStride;
            int offsetB = 2 * channelStride;

            // 将图像数据复制到张量中 (CHW 格式)
            for (int y = 0; y < _inputHeight; y++)
            {
                int rowOffset = y * _inputWidth;
                for (int x = 0; x < _inputWidth; x++)
                {

                    var pixel = rgbImage.At<Vec3f>(y, x);
                    int pixelIndex = rowOffset + x;
                    tensorSpan[offsetR + pixelIndex] = T.CreateSaturating(pixel.Item0);
                    tensorSpan[offsetG + pixelIndex] = T.CreateSaturating(pixel.Item1);
                    tensorSpan[offsetB + pixelIndex] = T.CreateSaturating(pixel.Item2);
                }
            }

            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(_inputName, inputTensor) };
            return inputs;
        }

        /// <summary>
        /// 后处理：解析模型输出，执行 NMS，将坐标映射回原图尺寸。
        /// </summary>
        private List<DetectionResult> PostprocessOutput<T>(
                Microsoft.ML.OnnxRuntime.Tensors.Tensor<T> outputTensor,
                int originalWidth,
                int originalHeight)
                where T : unmanaged, INumber<T>, IMinMaxValue<T>
        {
            var detections = new List<DetectionResult>();

            int numClasses = _classNames.Length;
            // 判断输出形状：YOLOv8 常见为 (1, 84, 8400) 或 (1, 8400, 84)
            var dims = outputTensor.Dimensions;
            bool isChwLayout = dims[1] == 4 + numClasses; // 若第1维是84，则布局为 [1, 84, 8400]
            int dimensions = isChwLayout ? dims[1] : dims[2];
            int numAnchors = isChwLayout ? dims[2] : dims[1];

            if (dimensions != 4 + numClasses)
            {
                throw new InvalidOperationException($"模型输出维度 ({dimensions}) 与提供的类别数量 ({numClasses}) 不匹配 (应为 4 + {numClasses})。");
            }

            // 计算缩放和偏移量（使用 float 精度，因为涉及像素尺寸）

            float offsetX = 0;
            float offsetY = 0;
            float scaleX = 1;
            float scaleY = 1;

            if (_outputHeight != -1 && _outputWidth != -1)
            {
                float scale = Math.Min((float)_outputWidth / originalWidth, (float)_outputHeight / originalHeight);
                float scaledWidth = originalWidth * scale;
                float scaledHeight = originalHeight * scale;

                offsetX = (_outputWidth - scaledWidth) / 2f;
                offsetY = (_outputHeight - scaledHeight) / 2f;
                scaleX = originalWidth / scaledWidth;
                scaleY = originalHeight / scaledHeight;
            }

            // 将阈值转换为 T 类型，只做一次
            T confidenceThreshold = T.CreateSaturating(_confidenceThreshold);
            // 零值常量
            T zero = T.CreateSaturating(0);
            T half = T.CreateSaturating(0.5f); // 用于 x1 = cx - w/2 时的除法

            // 获取张量底层 Span<T>，加快访问
            Span<T> span;
            if (outputTensor is DenseTensor<T> denseTensor)
            {
                // 情况1：可以直接获取 Span
                span = denseTensor.Buffer.Span;
            }
            else
            {
                // 情况2：非DenseTensor，无法直接获取Span，需分配数组作为后备缓冲区。
                // 权衡：这种情况极少见，因此性能损失通常可以接受。
                var array = new T[outputTensor.Length];
                // 手动将数据复制到数组中 (这里使用多维索引器，对任何 Tensor<T> 都有效)
                int flatIndex = 0;
                if (dims.Length == 3)
                {
                    for (int i0 = 0; i0 < dims[0]; i0++)
                        for (int i1 = 0; i1 < dims[1]; i1++)
                            for (int i2 = 0; i2 < dims[2]; i2++)
                                array[flatIndex++] = outputTensor[i0, i1, i2];
                }
                else
                {
                    // 处理其他维度数量的情况，保持健壮性
                    throw new NotSupportedException($"仅支持3维张量，当前为 {dims.Length} 维。");
                }
                span = array.AsSpan();
            }

            // 计算单个 anchor 的步长（每行或每个 anchor 的数据长度）
            int anchorStride = dimensions; // 84

            for (int i = 0; i < numAnchors; i++)
            {
                // 🚀 性能优化：直接索引，完全避免创建任何临时数组或 stackalloc
                int startIdx = i * dimensions;
                // 注意：对于 CHW 布局，下面的 anchorData 不是一个连续的切片，但索引器可以正确处理跨步访问。
                // 为了代码清晰，我们保持使用多维索引器来处理 CHW 布局。
                Span<T> anchorData = isChwLayout ? default : span.Slice(startIdx, dimensions);

                // 读取 bbox 坐标
                T cx, cy, w, h;
                if (isChwLayout)
                {
                    // 对于 CHW 布局，直接通过计算好的索引访问
                    cx = span[0 * numAnchors + i];
                    cy = span[1 * numAnchors + i];
                    w = span[2 * numAnchors + i];
                    h = span[3 * numAnchors + i];
                }
                else
                {
                    cx = anchorData[0];
                    cy = anchorData[1];
                    w = anchorData[2];
                    h = anchorData[3];
                }

                // 找到最大置信度及其类别索引
                T maxConfidence = T.Zero;
                int maxClassIndex = -1;
                for (int k = 4; k < dimensions; k++)
                {
                    T conf = isChwLayout ? span[k * numAnchors + i] : anchorData[k];
                    if (conf > maxConfidence)
                    {
                        maxConfidence = conf;
                        maxClassIndex = k - 4;
                    }
                }

                // 置信度筛选
                if (maxConfidence < confidenceThreshold)
                    continue;

                // 将 cx, cy, w, h 转换为 x1, y1, x2, y2 (在模型输入尺寸内)
                // 注意：这里需要浮点运算，所以先将 T 转为 float 计算，最后用 CreateSaturating 转回 T
                float fCx = float.CreateSaturating(cx);
                float fCy = float.CreateSaturating(cy);
                float fW = float.CreateSaturating(w);
                float fH = float.CreateSaturating(h);

                float fX1 = fCx - fW / 2f;
                float fY1 = fCy - fH / 2f;
                float fX2 = fCx + fW / 2f;
                float fY2 = fCy + fH / 2f;

                // 显示推理框
                //Console.WriteLine($"推理框: cx={fCx}, cy={fCy}, w={fW}, h={fH}, x1={fX1}, y1={fY1}, x2={fX2}, y2={fY2}, conf={maxConfidence}, class={maxClassIndex}");

                // 映射回原图坐标
                fX1 = (fX1 - offsetX) * scaleX;
                fY1 = (fY1 - offsetY) * scaleY;
                fX2 = (fX2 - offsetX) * scaleX;
                fY2 = (fY2 - offsetY) * scaleY;

                // 边界裁剪
                fX1 = Math.Clamp(fX1, 0f, originalWidth);
                fY1 = Math.Clamp(fY1, 0f, originalHeight);
                fX2 = Math.Clamp(fX2, 0f, originalWidth);
                fY2 = Math.Clamp(fY2, 0f, originalHeight);

                // 构造检测结果（置信度等需用 float 存储，因为结果类可能要求 float）
                detections.Add(new DetectionResult
                {
                    TileType = (TileType)maxClassIndex,
                    //ClassName = _classNames[maxClassIndex],
                    Confidence = float.CreateSaturating(maxConfidence),
                    BoundingBox = new Rect(
                        (int)fX1, (int)fY1,
                        (int)(fX2 - fX1), (int)(fY2 - fY1))
                });
            }

            return ApplyNms(detections);
        }

        /// <summary>
        /// 对检测结果应用非极大值抑制 (NMS)。
        /// </summary>
        private List<DetectionResult> ApplyNms(List<DetectionResult> detections)
        {
            var nmsResults = new List<DetectionResult>();
            // 按置信度降序排序
            var sortedDetections = detections.OrderByDescending(d => d.Confidence).ToList();

            while (sortedDetections.Any())
            {
                var best = sortedDetections.First();
                nmsResults.Add(best);
                sortedDetections.RemoveAt(0);

                // 移除与当前最佳框 IoU 过高的框
                sortedDetections.RemoveAll(d =>
                {
                    var iou = CalculateIoU(best.BoundingBox, d.BoundingBox);
                    return iou > _iouThreshold;
                });
            }

            return nmsResults;
        }

        /// <summary>
        /// 计算两个矩形的交并比 (IoU)。
        /// </summary>
        private float CalculateIoU(Rect a, Rect b)
        {
            var intersection = Rect.Intersect(a, b);
            if (intersection.Width <= 0 || intersection.Height <= 0)
                return 0;

            float intersectionArea = intersection.Width * intersection.Height;
            float unionArea = (a.Width * a.Height) + (b.Width * b.Height) - intersectionArea;
            return intersectionArea / unionArea;
        }

        internal void SaveDetections(Mat image, List<DetectionResult> detections, string outputPath)
        {
            var annotatedImage = DrawDetections(image, detections);
            annotatedImage.SaveImage(outputPath);
        }

        internal Mat DrawDetections(Mat image, List<DetectionResult> detections)
        {
            // 1. 复制图像（可选，避免修改原图）
            Mat annotatedImage = image.Clone();

            var colorMap = ColorGenerator.GenerateColorMap(_classNames);

            foreach (var det in detections)
            {
                var color = colorMap[det.TileTypeId];

                // 2. 定义矩形区域
                var pt1 = new OpenCvSharp.Point(det.BoundingBox.X, det.BoundingBox.Y);
                var pt2 = new OpenCvSharp.Point(det.BoundingBox.X + det.BoundingBox.Width, det.BoundingBox.Y + det.BoundingBox.Height);

                // 3. 绘制边界框（这里用红色）
                Cv2.Rectangle(annotatedImage, pt1, pt2, color, 2);

                // 4. 准备标签文本
                string label = $"{det.TileName}: {det.Confidence:P1}"; // e.g., "person: 95.5%"

                // 5. 绘制标签背景
                int baseline;
                var textSize = Cv2.GetTextSize(label, HersheyFonts.HersheySimplex, 0.5, 1, out baseline);
                var labelBgPt1 = new OpenCvSharp.Point(det.BoundingBox.X, det.BoundingBox.Y - textSize.Height - 5);
                var labelBgPt2 = new OpenCvSharp.Point(det.BoundingBox.X + textSize.Width, det.BoundingBox.Y);
                // -1 表示填充矩形
                Cv2.Rectangle(annotatedImage, labelBgPt1, labelBgPt2, color, -1);

                // 6. 在背景上绘制文本
                var textPt = new OpenCvSharp.Point(det.BoundingBox.X, det.BoundingBox.Y - 5);
                Cv2.PutText(annotatedImage, label, textPt, HersheyFonts.HersheySimplex, 0.5, Scalar.White, 1);
            }
            return annotatedImage;
        }

        /// <summary>
        /// 释放资源。
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _session?.Dispose();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}