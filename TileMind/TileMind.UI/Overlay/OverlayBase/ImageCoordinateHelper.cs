using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TileMind.UI.Overlay.OverlayBase
{
    public static class ImageCoordinateHelper
    {
        /// <summary>
        /// 计算从原始图像坐标系到 Image 控件显示区域的变换矩阵
        /// </summary>
        /// <param name="image">Image 控件</param>
        /// <returns>变换矩阵</returns>
        public static Matrix GetImageTransformMatrix(Image image)
        {
            if (image.Source == null)
                return Matrix.Identity;

            double imageWidth = image.Source.Width;
            double imageHeight = image.Source.Height;
            double controlWidth = image.ActualWidth;
            double controlHeight = image.ActualHeight;

            if (controlWidth <= 0 || controlHeight <= 0 || imageWidth <= 0 || imageHeight <= 0)
                return Matrix.Identity;

            Matrix matrix = Matrix.Identity;

            switch (image.Stretch)
            {
                case Stretch.None:
                    // 图像按原始尺寸显示，可能有裁剪
                    matrix.Translate((controlWidth - imageWidth) / 2, (controlHeight - imageHeight) / 2);
                    break;

                case Stretch.Fill:
                    // 拉伸填充，完全缩放至控件尺寸
                    matrix.Scale(controlWidth / imageWidth, controlHeight / imageHeight);
                    break;

                case Stretch.Uniform:
                    // 等比缩放，保留黑边
                    double scale = Math.Min(controlWidth / imageWidth, controlHeight / imageHeight);
                    double scaledWidth = imageWidth * scale;
                    double scaledHeight = imageHeight * scale;
                    double offsetX = (controlWidth - scaledWidth) / 2;
                    double offsetY = (controlHeight - scaledHeight) / 2;
                    matrix.Scale(scale, scale);
                    matrix.Translate(offsetX, offsetY);
                    break;

                case Stretch.UniformToFill:
                    // 等比缩放填充，可能裁剪
                    double scaleFill = Math.Max(controlWidth / imageWidth, controlHeight / imageHeight);
                    double scaledWidthFill = imageWidth * scaleFill;
                    double scaledHeightFill = imageHeight * scaleFill;
                    double offsetXFill = (controlWidth - scaledWidthFill) / 2;
                    double offsetYFill = (controlHeight - scaledHeightFill) / 2;
                    matrix.Scale(scaleFill, scaleFill);
                    matrix.Translate(offsetXFill, offsetYFill);
                    break;
            }

            return matrix;
        }
    }
}
