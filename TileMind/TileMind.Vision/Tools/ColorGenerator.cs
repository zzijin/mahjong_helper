using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Text;

namespace TileMind.Vision.Tools
{
    public class ColorGenerator
    {
        /// <summary>
        /// 为指定数量的类别生成高区分度颜色（基于 HSV 色相均匀分布）
        /// </summary>
        /// <param name="classCount">类别总数</param>
        /// <returns>颜色列表，索引对应类别索引</returns>
        public static List<Scalar> GenerateDistinctColors(int classCount)
        {
            var colors = new List<Scalar>();
            for (int i = 0; i < classCount; i++)
            {
                // 色相均匀分布（OpenCV H 范围为 0～180）
                double hue = (i * 180.0 / classCount) % 180;
                byte h = (byte)hue;
                byte s = 255; // 饱和度
                byte v = 255; // 明度

                // 创建一个 1x1 的单像素 HSV 图像
                using (var hsvMat = new Mat(1, 1, MatType.CV_8UC3, new Scalar(h, s, v)))
                using (var bgrMat = new Mat())
                {
                    Cv2.CvtColor(hsvMat, bgrMat, ColorConversionCodes.HSV2BGR);

                    // 获取转换后的 BGR 颜色值
                    Vec3b colorVec = bgrMat.At<Vec3b>(0, 0);
                    colors.Add(new Scalar(colorVec.Item0, colorVec.Item1, colorVec.Item2));
                }
            }
            return colors;
        }

        /// <summary>
        /// 生成从类别名称到颜色的映射字典
        /// </summary>
        public static Dictionary<T, Scalar> GenerateColorMap<T>(IList<T> classNames) where T : notnull
        {
            var colors = GenerateDistinctColors(classNames.Count);
            var map = new Dictionary<T, Scalar>();
            for (int i = 0; i < classNames.Count; i++)
            {
                map[classNames[i]] = colors[i];
            }
            return map;
        }
    }
}
