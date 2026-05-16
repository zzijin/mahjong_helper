using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using TileMind.Common.Models;

namespace TileMind.Vision.Detection
{
    internal static class YoloDetector_Float16
    {
        internal static float Float16BitsToSingle(this YoloDetector yoloDetector, ushort hbits)
        {
            int s = (hbits >> 15) & 0x00000001;
            int e = (hbits >> 10) & 0x0000001f;
            int f = hbits & 0x000003ff;

            if (e == 0)
            {
                if (f == 0)
                {
                    return BitConverter.Int32BitsToSingle(s << 31);
                }
                while ((f & 0x00000400) == 0)
                {
                    f <<= 1;
                    e -= 1;
                }
                e += 1;
                f &= ~0x00000400;
            }
            else if (e == 31)
            {
                if (f == 0)
                    return BitConverter.Int32BitsToSingle((s << 31) | 0x7f800000);
                else
                    return BitConverter.Int32BitsToSingle((s << 31) | 0x7f800000 | (f << 13));
            }

            e = e + (127 - 15);
            int mant = f << 13;
            int bits = (s << 31) | (e << 23) | mant;
            return BitConverter.Int32BitsToSingle(bits);
        }

        internal static float Float16ToSingle(this YoloDetector yoloDetector, Microsoft.ML.OnnxRuntime.Float16 v)
        {
            Span<Microsoft.ML.OnnxRuntime.Float16> tmp = MemoryMarshal.CreateSpan(ref v, 1);
            Span<ushort> asU16 = MemoryMarshal.Cast<Microsoft.ML.OnnxRuntime.Float16, ushort>(tmp);
            ushort bits = asU16[0];
            return yoloDetector.Float16BitsToSingle(bits);
        }

        internal static List<NamedOnnxValue> PreprocessImageFloat(this YoloDetector yoloDetector, Mat image,string inputName, int inputHeight, int inputWidth)
        {
            var letterboxedImage = new Mat(inputHeight, inputWidth, MatType.CV_8UC3, Scalar.Gray);
            var scale = Math.Min((float)inputHeight / image.Width, (float)inputWidth / image.Height);
            var scaledWidth = (int)(image.Width * scale);
            var scaledHeight = (int)(image.Height * scale);
            var offsetX = (inputWidth - scaledWidth) / 2;
            var offsetY = (inputHeight - scaledHeight) / 2;

            using var resizedImage = new Mat();
            Cv2.Resize(image, resizedImage, new Size(scaledWidth, scaledHeight));
            resizedImage.CopyTo(new Mat(letterboxedImage, new Rect(offsetX, offsetY, scaledWidth, scaledHeight)));

            using var rgbImage = new Mat();
            Cv2.CvtColor(letterboxedImage, rgbImage, ColorConversionCodes.BGR2RGB);
            rgbImage.ConvertTo(rgbImage, MatType.CV_32FC3, 1.0 / 255.0);

            var inputTensor = new DenseTensor<Float16>(new[] { 1, 3, inputHeight, inputWidth });
            var tensorSpan = inputTensor.Buffer.Span;

            int channelStride = inputHeight * inputWidth;
            int offsetR = 0 * channelStride;
            int offsetG = 1 * channelStride;
            int offsetB = 2 * channelStride;

            for (int y = 0; y < inputHeight; y++)
            {
                int rowOffset = y * inputWidth;
                for (int x = 0; x < inputWidth; x++)
                {
                    var pixel = rgbImage.At<Vec3f>(y, x);
                    int pixelIndex = rowOffset + x;
                    tensorSpan[offsetR + pixelIndex] = (Float16)pixel.Item0;
                    tensorSpan[offsetG + pixelIndex] = (Float16)pixel.Item1;
                    tensorSpan[offsetB + pixelIndex] = (Float16)pixel.Item2;
                }
            }

            return new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, inputTensor) };
        }

        internal static List<DetectionResult> PostprocessOutputFloat16(
            this YoloDetector yoloDetector,
            Tensor<Microsoft.ML.OnnxRuntime.Float16> outputTensor,
            int originalWidth,
            int originalHeight,
            string[] classNames,
            float confidenceThreshold,
            int outputWidth,
            int outputHeight,
            Func<List<DetectionResult>, List<DetectionResult>> applyNms)
        {
            var detections = new List<DetectionResult>();

            int numClasses = classNames.Length;
            var dims = outputTensor.Dimensions;
            if (dims.Length != 3)
                throw new NotSupportedException($"仅支持3维张量，当前为 {dims.Length} 维。");

            bool isChwLayout = dims[1] == 4 + numClasses;
            int dimensions = isChwLayout ? dims[1] : dims[2];
            int numAnchors = isChwLayout ? dims[2] : dims[1];

            if (dimensions != 4 + numClasses)
                throw new InvalidOperationException($"模型输出维度 ({dimensions}) 与类别数量 ({numClasses}) 不匹配 (应为 4 + {numClasses})。");

            float scale = Math.Min((float)outputWidth / originalWidth, (float)outputHeight / originalHeight);
            float scaledWidth = originalWidth * scale;
            float scaledHeight = originalHeight * scale;
            float offsetX = (outputWidth - scaledWidth) / 2f;
            float offsetY = (outputHeight - scaledHeight) / 2f;
            float scaleX = originalWidth / scaledWidth;
            float scaleY = originalHeight / scaledHeight;

            // 优先走 DenseTensor 快路径（避免全量拷贝）
            if (outputTensor is DenseTensor<Microsoft.ML.OnnxRuntime.Float16> dense)
            {
                var span = dense.Buffer.Span;
                for (int i = 0; i < numAnchors; i++)
                {
                    int startIdx = i * dimensions;
                    float cx, cy, w, h;
                    if (isChwLayout)
                    {
                        cx = yoloDetector.Float16ToSingle(span[0 * numAnchors + i]);
                        cy = yoloDetector.Float16ToSingle(span[1 * numAnchors + i]);
                        w = yoloDetector.Float16ToSingle(span[2 * numAnchors + i]);
                        h = yoloDetector.Float16ToSingle(span[3 * numAnchors + i]);
                    }
                    else
                    {
                        cx = yoloDetector.Float16ToSingle(span[startIdx + 0]);
                        cy = yoloDetector.Float16ToSingle(span[startIdx + 1]);
                        w = yoloDetector.Float16ToSingle(span[startIdx + 2]);
                        h = yoloDetector.Float16ToSingle(span[startIdx + 3]);
                    }

                    float maxConfidence = 0f;
                    int maxClassIndex = -1;
                    for (int k = 4; k < dimensions; k++)
                    {
                        float conf = isChwLayout ? yoloDetector.Float16ToSingle(span[k * numAnchors + i]) : yoloDetector.Float16ToSingle(span[startIdx + k]);
                        if (conf > maxConfidence)
                        {
                            maxConfidence = conf;
                            maxClassIndex = k - 4;
                        }
                    }

                    if (maxConfidence < confidenceThreshold)
                        continue;

                    float fX1 = cx - w / 2f;
                    float fY1 = cy - h / 2f;
                    float fX2 = cx + w / 2f;
                    float fY2 = cy + h / 2f;

                    fX1 = (fX1 - offsetX) * scaleX;
                    fY1 = (fY1 - offsetY) * scaleY;
                    fX2 = (fX2 - offsetX) * scaleX;
                    fY2 = (fY2 - offsetY) * scaleY;

                    fX1 = Math.Clamp(fX1, 0f, originalWidth);
                    fY1 = Math.Clamp(fY1, 0f, originalHeight);
                    fX2 = Math.Clamp(fX2, 0f, originalWidth);
                    fY2 = Math.Clamp(fY2, 0f, originalHeight);

                    detections.Add(new DetectionResult
                    {
                        TileType = (TileType)maxClassIndex,
                        //ClassName = classNames[maxClassIndex],
                        Confidence = maxConfidence,
                        BoundingBox = new OpenCvSharp.Rect((int)fX1, (int)fY1, (int)(fX2 - fX1), (int)(fY2 - fY1))
                    });
                }
            }
            else
            {
                // 非 Dense 情况：按需读取并即时转换，避免整张转换
                for (int i = 0; i < numAnchors; i++)
                {
                    int startIdx = i * dimensions;
                    float cx, cy, w, h;
                    if (isChwLayout)
                    {
                        cx = yoloDetector.Float16ToSingle(outputTensor[0, 0 * numAnchors + i]);
                        cy = yoloDetector.Float16ToSingle(outputTensor[0, 1 * numAnchors + i]);
                        w = yoloDetector.Float16ToSingle(outputTensor[0, 2 * numAnchors + i]);
                        h = yoloDetector.Float16ToSingle(outputTensor[0, 3 * numAnchors + i]);
                    }
                    else
                    {
                        cx = yoloDetector.Float16ToSingle(outputTensor[0, i, 0]);
                        cy = yoloDetector.Float16ToSingle(outputTensor[0, i, 1]);
                        w = yoloDetector.Float16ToSingle(outputTensor[0, i, 2]);
                        h = yoloDetector.Float16ToSingle(outputTensor[0, i, 3]);
                    }

                    float maxConfidence = 0f;
                    int maxClassIndex = -1;
                    for (int k = 4; k < dimensions; k++)
                    {
                        float conf = isChwLayout ? yoloDetector.Float16ToSingle(outputTensor[0, k * numAnchors + i]) : yoloDetector.Float16ToSingle(outputTensor[0, i, k]);
                        if (conf > maxConfidence)
                        {
                            maxConfidence = conf;
                            maxClassIndex = k - 4;
                        }
                    }

                    if (maxConfidence < confidenceThreshold)
                        continue;

                    float fX1 = cx - w / 2f;
                    float fY1 = cy - h / 2f;
                    float fX2 = cx + w / 2f;
                    float fY2 = cy + h / 2f;

                    fX1 = (fX1 - offsetX) * scaleX;
                    fY1 = (fY1 - offsetY) * scaleY;
                    fX2 = (fX2 - offsetX) * scaleX;
                    fY2 = (fY2 - offsetY) * scaleY;

                    fX1 = Math.Clamp(fX1, 0f, originalWidth);
                    fY1 = Math.Clamp(fY1, 0f, originalHeight);
                    fX2 = Math.Clamp(fX2, 0f, originalWidth);
                    fY2 = Math.Clamp(fY2, 0f, originalHeight);

                    detections.Add(new DetectionResult
                    {
                        TileType = (TileType)maxClassIndex,
                        //ClassName = classNames[maxClassIndex],
                        Confidence = maxConfidence,
                        BoundingBox = new OpenCvSharp.Rect((int)fX1, (int)fY1, (int)(fX2 - fX1), (int)(fY2 - fY1))
                    });
                }
            }

            return applyNms != null ? applyNms(detections) : detections;
        }
    }
}
