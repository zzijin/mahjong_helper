using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TileMind.UI.ViewModels;

namespace TileMind.UI.Views
{
    /// <summary>
    /// ScreenSplitterOverlayControl.xaml 的交互逻辑
    /// </summary>
    public partial class ScreenSplitterOverlayControl : UserControl
    {
        ScreenSplitterViewModel _screenSplitterViewModel;

        public Point[] DoraIndicatorArea { get; private set ; }
        public Point[] TableArea { get; private set ; }
        public Point[] DiscardPondArea { get; private set ; }
        public Point[] InfoArea { get; private set ; }

        public ScreenSplitterOverlayControl(ScreenSplitterViewModel screenSplitterViewModel)
        {
            _screenSplitterViewModel = screenSplitterViewModel;
            DataContext = this;

            InitializeComponent();
            Loaded += (s, e) => DrawDivisionLines();
        }

        private void OnAnyVertexChanged(QuadrilateralControl quad)
        {
            DrawDivisionLines();
        }

        /// <summary>
        /// 绘制所有分割线：
        /// - 四边形B的两条对角线
        /// - 四边形B四条边延长至A边界的线段
        /// </summary>
        private void DrawDivisionLines()
        {
            LineCanvas.Children.Clear();
            // 获取四个四边形的顶点
            Point[] a = { QuadA.TopLeft, QuadA.TopRight, QuadA.BottomRight, QuadA.BottomLeft };
            Point[] b = { QuadB.TopLeft, QuadB.TopRight, QuadB.BottomRight, QuadB.BottomLeft };
            Point[] c = { QuadC.TopLeft, QuadC.TopRight, QuadC.BottomRight, QuadC.BottomLeft };
            Point[] d = { QuadD.TopLeft, QuadD.TopRight, QuadD.BottomRight, QuadD.BottomLeft };

            // 1. 四边形B的四条对角线
            DrawLine(b[0], d[0], Colors.Red, 1.5);
            DrawLine(b[1], d[1], Colors.Red, 1.5);
            DrawLine(b[2], d[2], Colors.Red, 1.5);
            DrawLine(b[3], d[3], Colors.Red, 1.5);

            // 2. 四边形B四条边延长至A边界
            // 上边（P1->P0）延长到A边界
            var intersectA = DrawExtendedLineToBoundary(b[1], b[0], a, Colors.Blue, 1);
            // 下边（P2->P1）延长到A边界
            var intersectB = DrawExtendedLineToBoundary(b[2], b[1], a, Colors.Blue, 1);
            // 左边（P3->P2）延长到A边界
            var intersectC = DrawExtendedLineToBoundary(b[3], b[2], a, Colors.Blue, 1);
            // 右边（P0->P3）延长到A边界
            var intersectD = DrawExtendedLineToBoundary(b[0], b[3], a, Colors.Blue, 1);
            // 可选：绘制四边形D对角线或轮廓（可根据需要添加）
        }

        private void DrawLine(Point start, Point end, Color color, double thickness)
        {
            var line = new Line
            {
                X1 = start.X,
                Y1 = start.Y,
                X2 = end.X,
                Y2 = end.Y,
                Stroke = new SolidColorBrush(color),
                StrokeThickness = thickness
            };
            LineCanvas.Children.Add(line);
        }

        /// <summary>
        /// 从起点 start 沿方向 dir 发射射线，找到与多边形 polygon 的第一个交点（正方向），
        /// 并绘制从 start 到交点的线段。
        /// </summary>
        private Point DrawExtendedLineToBoundary(Point start, Point end, Point[] polygon, Color color, double thickness)
        {
            Point intersect;
            Vector dir = end - start;
            if (dir.LengthSquared < 0.001) return intersect;

            // 寻找射线与多边形各边的交点，且要求交点在射线的正方向上（t >= 0）

            for (int i = 0; i < 4; i++)
            {
                Point edgeStart = polygon[i];
                Point edgeEnd = polygon[(i + 1) % 4];

                if (RayIntersectsSegment(start, end, edgeStart, edgeEnd, out intersect))
                {
                    DrawLine(end, intersect, color, thickness);
                }
            }
            return intersect;
        }

        /// <summary>
        /// 计算射线与线段的交点。
        /// </summary>
        /// <param name="rayStart">射线起点 A</param>
        /// <param name="rayPoint">射线经过点 B（确定方向）</param>
        /// <param name="segStart">线段端点 C</param>
        /// <param name="segEnd">线段端点 D</param>
        /// <param name="intersection">输出的交点坐标（若相交）</param>
        /// <returns>true: 相交，并返回交点；false: 不相交</returns>
        public static bool RayIntersectsSegment(Point rayStart, Point rayPoint,
                                                 Point segStart, Point segEnd,
                                                 out Point intersection)
        {
            intersection = new Point(double.NaN, double.NaN);

            // 向量定义
            Vector v = rayPoint - rayStart;          // 射线方向向量
            Vector w = segEnd - segStart;            // 线段方向向量
            Vector ac = segStart - rayStart;         // 线段起点到射线起点的向量

            // 叉积计算
            double crossVW = Vector.CrossProduct(v, w);  // V × W
            double crossAC_W = Vector.CrossProduct(ac, w);
            double crossAC_V = Vector.CrossProduct(ac, v);

            // 处理平行情况（考虑浮点误差）
            const double epsilon = 1e-10;
            if (Math.Abs(crossVW) < epsilon)
            {
                // 平行或共线，此处简单处理为不相交
                return false;
            }

            // 参数 t 和 u
            double t = crossAC_W / crossVW;
            double u = crossAC_V / crossVW;

            // 判断是否在射线正向和线段范围内
            if (t >= 0 && u >= 0 && u <= 1)
            {
                intersection = rayStart + t * v;
                return true;
            }

            return false;
        }

        // ---------- 公共方法：获取各区域顶点坐标 ----------
        public (Point[] A, Point[] B, Point[] C, Point[] D) GetAllRegionVertices()
        {
            return (
                new[] { QuadA.TopLeft, QuadA.TopRight, QuadA.BottomLeft, QuadA.BottomRight },
                new[] { QuadB.TopLeft, QuadB.TopRight, QuadB.BottomLeft, QuadB.BottomRight },
                new[] { QuadC.TopLeft, QuadC.TopRight, QuadC.BottomLeft, QuadC.BottomRight },
                new[] { QuadD.TopLeft, QuadD.TopRight, QuadD.BottomLeft, QuadD.BottomRight }
            );
        }

        // 可进一步提供计算玩家牌河区域（四个梯形/四边形）和手牌区域的方法


        // 相对坐标转换为绝对坐标（相对于整个屏幕）
        public Point[] ToAbsolute(Point[] relativePoints)
        {
            // 这里假设 QuadA 是整个屏幕的边界
            Point[] absolutePoints = new Point[relativePoints.Length];
            for (int i = 0; i < relativePoints.Length; i++)
            {
                Point abs = PointToScreen(relativePoints[i]);
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
                Point rel = PointFromScreen(absolutePoints[i]);
                relativePoints[i] = rel;
            }
            return relativePoints;
        }
    }
}
