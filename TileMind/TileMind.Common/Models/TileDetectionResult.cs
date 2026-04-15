using System;
using System.Collections.Generic;
using System.Text;
using TileMind.Common.Helpers;

namespace TileMind.Common.Models
{
    /// <summary>
    /// 检测结果类。
    /// </summary>
    public record class TileDetectionResult
    {
        // 类型ID
        public TileType TileType { get; set; }
        //置信度
        public float Confidence { get; set; }
        //边框
        public Rect BoundingBox { get; set; }

        //类型名称
        public string TileName => TileType.GetTileName();
    }
}
