using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using TileMind.Common.Config;

namespace TileMind.UI.Views;

public partial class ScreenSplitterOverlayControl : UserControl
{
    private Point _intersectA, _intersectB, _intersectC, _intersectD;

    public ScreenSplitterOverlayControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        DrawDivisionLines();
    }

    private void OnAnyVertexChanged(QuadrilateralControl quad)
    {
        DrawDivisionLines();
    }

    /// <summary>
    /// 将所有区域写入 ScreenCaptureOptions（WPF 相对坐标 → 屏幕绝对坐标 → OpenCvSharp）。
    /// </summary>
    public void WriteToOptions(ScreenCaptureOptions options)
    {
        options.DoraIndicatorArea = ToOpenCvPoints(QuadC);
        options.InfoArea = ToOpenCvPoints(QuadD);
        options.TableArea = ToOpenCvPoints(QuadA);
        // DiscardPondArea 基区域 = QuadB（中央弃牌区外边界，仅供参考）
        options.DiscardPondArea = ToOpenCvPoints(QuadB);

        // 四个玩家弃牌区 = QuadB 与 QuadD 之间的梯形
        options.SelfDiscardPondArea = ToOpenCvPoints(
            QuadB.BottomLeft, QuadD.BottomLeft, QuadD.BottomRight, QuadB.BottomRight);
        options.RightDiscardPondArea = ToOpenCvPoints(
            QuadB.BottomRight, QuadD.BottomRight, QuadD.TopRight, QuadB.TopRight);
        options.OppositeDiscardPondArea = ToOpenCvPoints(
            QuadB.TopRight, QuadD.TopRight, QuadD.TopLeft, QuadB.TopLeft);
        options.LeftDiscardPondArea = ToOpenCvPoints(
            QuadB.TopLeft, QuadD.TopLeft, QuadD.BottomLeft, QuadB.BottomLeft);

        // 四个玩家手牌+副露区 = QuadA 与 QuadB 之间的梯形（由延长线交点分割）
        options.LeftHandAndMeldArea = ToOpenCvPoints(
            _intersectA, QuadB.TopLeft, QuadB.BottomLeft, _intersectD);
        options.OppositeHandAndMeldArea = ToOpenCvPoints(
            _intersectB, QuadB.TopRight, QuadB.TopLeft, _intersectA);
        options.RightHandAndMeldArea = ToOpenCvPoints(
            _intersectC, QuadB.BottomRight, QuadB.TopRight, _intersectB);
        options.SelfHandAndMeldArea = ToOpenCvPoints(
            _intersectD, QuadB.BottomLeft, QuadB.BottomRight, _intersectC);
    }

    /// <summary>
    /// 从 ScreenCaptureOptions 加载配置 → 设置各 Quad 顶点位置。
    /// </summary>
    public void LoadFromOptions(ScreenCaptureOptions options)
    {
        if (!IsRegionConfigured(options.TableArea)) return;

        SetQuadVertices(QuadA, options.TableArea);
        SetQuadVertices(QuadB, options.DiscardPondArea);
        SetQuadVertices(QuadC, options.DoraIndicatorArea);
        SetQuadVertices(QuadD, options.InfoArea);

        DrawDivisionLines();
    }

    // ─────────────── 绘制分割线 ───────────────

    private void DrawDivisionLines()
    {
        LineCanvas.Children.Clear();

        Point[] a = { QuadA.TopLeft, QuadA.TopRight, QuadA.BottomRight, QuadA.BottomLeft };
        Point[] b = { QuadB.TopLeft, QuadB.TopRight, QuadB.BottomRight, QuadB.BottomLeft };
        Point[] c = { QuadC.TopLeft, QuadC.TopRight, QuadC.BottomRight, QuadC.BottomLeft };
        Point[] d = { QuadD.TopLeft, QuadD.TopRight, QuadD.BottomRight, QuadD.BottomLeft };

        // 红对角线: B 四角 → D 对应角
        DrawLine(b[0], d[0], Colors.Red, 1.5);
        DrawLine(b[1], d[1], Colors.Red, 1.5);
        DrawLine(b[2], d[2], Colors.Red, 1.5);
        DrawLine(b[3], d[3], Colors.Red, 1.5);

        // 蓝延长线: B 各边 → QuadA 边界
        _intersectA = DrawExtendedLineToBoundary(b[1], b[0], a, Colors.Blue, 1);
        _intersectB = DrawExtendedLineToBoundary(b[2], b[1], a, Colors.Blue, 1);
        _intersectC = DrawExtendedLineToBoundary(b[3], b[2], a, Colors.Blue, 1);
        _intersectD = DrawExtendedLineToBoundary(b[0], b[3], a, Colors.Blue, 1);
    }

    private void DrawLine(Point start, Point end, Color color, double thickness)
    {
        LineCanvas.Children.Add(new Line
        {
            X1 = start.X, Y1 = start.Y,
            X2 = end.X, Y2 = end.Y,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = thickness
        });
    }

    private Point DrawExtendedLineToBoundary(Point start, Point end, Point[] polygon, Color color, double thickness)
    {
        Point intersect = default;
        Vector dir = end - start;
        if (dir.LengthSquared < 0.001) return intersect;

        for (int i = 0; i < 4; i++)
        {
            if (RayIntersectsSegment(start, end, polygon[i], polygon[(i + 1) % 4], out intersect))
                DrawLine(end, intersect, color, thickness);
        }
        return intersect;
    }

    public static bool RayIntersectsSegment(Point rayStart, Point rayPoint,
                                             Point segStart, Point segEnd,
                                             out Point intersection)
    {
        intersection = new Point(double.NaN, double.NaN);
        Vector v = rayPoint - rayStart;
        Vector w = segEnd - segStart;
        Vector ac = segStart - rayStart;

        double crossVW = Vector.CrossProduct(v, w);
        const double epsilon = 1e-10;
        if (Math.Abs(crossVW) < epsilon) return false;

        double t = Vector.CrossProduct(ac, w) / crossVW;
        double u = Vector.CrossProduct(ac, v) / crossVW;

        if (t >= 0 && u >= 0 && u <= 1)
        {
            intersection = rayStart + t * v;
            return true;
        }
        return false;
    }

    // ─────────────── 坐标转换 ───────────────

    /// <summary>WPF 相对坐标 → 屏幕绝对坐标（double）</summary>
    public Point[] ToAbsolute(Point[] relative)
    {
        Point[] abs = new Point[relative.Length];
        for (int i = 0; i < relative.Length; i++)
            abs[i] = PointToScreen(relative[i]);
        return abs;
    }

    /// <summary>屏幕绝对坐标 → WPF 相对坐标（double）</summary>
    public Point[] ToRelative(Point[] absolute)
    {
        Point[] rel = new Point[absolute.Length];
        for (int i = 0; i < absolute.Length; i++)
            rel[i] = PointFromScreen(absolute[i]);
        return rel;
    }

    /// <summary>WPF 相对坐标 → OpenCvSharp.Point[]（经屏幕绝对坐标）</summary>
    private OpenCvSharp.Point[] ToOpenCvPoints(Point p0, Point p1, Point p2, Point p3)
        => ToOpenCvPoints(new[] { p0, p1, p2, p3 });

    private OpenCvSharp.Point[] ToOpenCvPoints(QuadrilateralControl quad)
        => ToOpenCvPoints(new[] { quad.TopLeft, quad.TopRight, quad.BottomRight, quad.BottomLeft });

    private OpenCvSharp.Point[] ToOpenCvPoints(Point[] wpfRelative)
    {
        var screen = ToAbsolute(wpfRelative);
        var cv = new OpenCvSharp.Point[screen.Length];
        for (int i = 0; i < screen.Length; i++)
            cv[i] = new OpenCvSharp.Point((int)Math.Round(screen[i].X), (int)Math.Round(screen[i].Y));
        return cv;
    }

    /// <summary>OpenCvSharp.Point[] → 设置 Quad 顶点（经屏幕绝对坐标 → WPF 相对）</summary>
    private void SetQuadVertices(QuadrilateralControl quad, OpenCvSharp.Point[] cvPoints)
    {
        if (cvPoints.Length != 4) return;
        var screen = new Point[cvPoints.Length];
        for (int i = 0; i < cvPoints.Length; i++)
            screen[i] = new Point(cvPoints[i].X, cvPoints[i].Y);
        var rel = ToRelative(screen);
        quad.TopLeft = rel[0];
        quad.TopRight = rel[1];
        quad.BottomRight = rel[2];
        quad.BottomLeft = rel[3];
    }

    private static bool IsRegionConfigured(OpenCvSharp.Point[] quad)
        => quad.Length == 4 && !quad.All(p => p.X == 0 && p.Y == 0);
}
