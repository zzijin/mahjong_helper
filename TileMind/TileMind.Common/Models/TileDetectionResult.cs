using System;
using System.Collections.Generic;
using System.Text;

namespace TileMind.Common.Models
{
    /// <summary>
    /// 检测结果类。
    /// </summary>
    public record class TileDetectionResult
    {
        // 类型ID
        public int TileId { get; set; }
        //类型名称
        public required string TileName { get; set; }
        //置信度
        public float Confidence { get; set; }
        //边框
        public Rect BoundingBox { get; set; }
    }
}
