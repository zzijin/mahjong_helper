using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Text;

namespace TileMind.Vision.Detection
{
    internal class DetectionResult
    {
        // 类型ID
        public int ClassId { get; set; }
        //类型名称
        public required string ClassName { get; set; }
        //置信度
        public float Confidence { get; set; }
        //边框
        public Rect BoundingBox { get; set; }
    }
}
