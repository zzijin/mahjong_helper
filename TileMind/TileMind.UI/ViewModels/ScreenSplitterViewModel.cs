using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using TileMind.Common.Config;
using TileMind.UI.Views;

namespace TileMind.UI.ViewModels
{
    public class ScreenSplitterViewModel : ViewModel
    {
        ScreenSplitterOverlayControl _control;
        ScreenCaptureOptions _options;
        public ScreenSplitterViewModel(ScreenSplitterOverlayControl control, ScreenCaptureOptions options)
        {
            _control = control;
            _options = options;
        }


        // 相对坐标转换为绝对坐标（相对于整个屏幕）
        public Point[] ToAbsolute(Point[] relativePoints)
        {
            // 这里假设 QuadA 是整个屏幕的边界
            Point[] absolutePoints = new Point[relativePoints.Length];
            for (int i = 0; i < relativePoints.Length; i++)
            {
                Point abs = _control.PointToScreen(relativePoints[i]);
                absolutePoints[i] = abs;
            }
            return absolutePoints;
        }

        // 绝对坐标转换为相对坐标（相对于本控件）
        public Point[] ToRelative(Point[] absolutePoints)
        {
            Point[] relativePoints = new Point[absolutePoints.Length];
            for (int i = 0; i < absolutePoints.Length; i++)
            {
                Point rel = _control.PointFromScreen(absolutePoints[i]);
                relativePoints[i] = rel;
            }
            return relativePoints;
        }
    }
}
