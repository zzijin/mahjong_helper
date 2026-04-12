using System;
using System.Collections.Generic;
using System.Text;

namespace TileMind.Common.Config
{
    public class YoloOptions
    {
        //模型地址
        public string ModelPath { get; set; } = @".\models\yolov8m-fp32.onnx";

        //模型支持的类别名称
        public string[] ClassNames { get; set; } = {
            "1m","2m","3m","4m","5m","6m","7m","8m","9m","0m",
            "1p","2p","3p","4p","5p","6p","7p","8p","9p","0p",
            "1s","2s","3s","4s","5s","6s","7s","8s","9s","0s",
            "1z","2z","3z","4z","5z","6z","7z" };

        //置信度
        public float ConfidenceThreshold { get; set; } = 0.40f;
        //IoU阈值
        public float IouThreshold { get; set; } = 0.50f;
        //GPU设备ID，若为-1则仅使用CPU
        public int GpuDeviceId { get; set; } = 0;
        //模型输入的图像尺寸(程序处理时使用的)
        public int InputSize { get; set; } = 1280;

        //检测器池的最小大小
        public int MinDetectorPoolSize { get; set; } = 5;
        //检测器池的最大大小
        public int MaxDetectorPoolSize { get; set; } = 10;
        //获取检测器实例的超时时间，单位秒
        public int RentTimeoutSeconds { get; set; } = 5;
    }
}
