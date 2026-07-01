using OpenCvSharp;
using System.Text.Json;
using System.Text.Json.Serialization;
using TileMind.Common.Helpers;
using PointF = System.Drawing.PointF;
using RectangleF = System.Drawing.RectangleF;

namespace TileMind.Common.Config
{
    public class ScreenCaptureOptions
    {
        public const string SettingFilePath = @".\settings\screencapturesettings.json";

        //DXGI 适配器索引，通常0表示主显卡
        public int AdapterIndex { get; set; } = 0;

        //DXGI 显示器索引，通常0表示主显示器
        public int OutputIndex { get; set; } = 0;

        //目标游戏进程名（不含 .exe），用于 WGC 按窗口捕获
        public string? GameProcessName { get; set; } = @"Jantama_MahjongSoul";

        // ──────── 区域比值（0~1，相对游戏窗口或屏幕，真相源） ────────
        // 默认值来自 4K(3840×2160) 典型雀魂布局换算

        /// <summary>宝牌指示区比值坐标</summary>
        public PointF[] DoraIndicatorRatio { get; set; } = new PointF[4]
        {
            new(0.00807f, 0.04352f),
            new(0.00807f, 0.13843f),
            new(0.15807f, 0.13843f),
            new(0.15755f, 0.04398f),
        };

        /// <summary>牌桌区域比值坐标</summary>
        public PointF[] TableRatio { get; set; } = new PointF[4]
        {
            new(0.18255f, 0.01713f),
            new(0.81719f, 0.01713f),
            new(0.99870f, 0.96481f),
            new(0.00052f, 0.96481f),
        };

        /// <summary>弃牌区域比值坐标</summary>
        public PointF[] DiscardPondRatio { get; set; } = new PointF[4]
        {
            new(0.24740f, 0.10556f),
            new(0.75104f, 0.11111f),
            new(0.85339f, 0.77083f),
            new(0.14089f, 0.77870f),
        };

        /// <summary>牌桌信息区域比值坐标</summary>
        public PointF[] InfoRatio { get; set; } = new PointF[4]
        {
            new(0.42240f, 0.29167f),
            new(0.57656f, 0.29259f),
            new(0.58411f, 0.49398f),
            new(0.41615f, 0.49259f),
        };

        // ──────── 计算产物（绝对坐标，不持久化） ────────

        /// <summary>宝牌指示区绝对坐标</summary>
        [JsonIgnore]
        public Point[] DoraIndicatorArea { get; set; } = new Point[4];

        /// <summary>牌桌区域绝对坐标</summary>
        [JsonIgnore]
        public Point[] TableArea { get; set; } = new Point[4];

        /// <summary>弃牌区域绝对坐标</summary>
        [JsonIgnore]
        public Point[] DiscardPondArea { get; set; } = new Point[4];

        /// <summary>牌桌信息区域绝对坐标</summary>
        [JsonIgnore]
        public Point[] InfoArea { get; set; } = new Point[4];

        //以下区域通过计算获取
        //本家手牌+副露区
        [JsonIgnore]
        public Point[] SelfHandAndMeldArea { get; set; } = new Point[4];
        //本家弃牌区
        [JsonIgnore]
        public Point[] SelfDiscardPondArea { get; set; } = new Point[4];

        //下家手牌+副露区
        [JsonIgnore]
        public Point[] RightHandAndMeldArea { get; set; } = new Point[4];
        //下家弃牌区
        [JsonIgnore]
        public Point[] RightDiscardPondArea { get; set; } = new Point[4];

        //对家手牌+副露区
        [JsonIgnore]
        public Point[] OppositeHandAndMeldArea { get; set; } = new Point[4];
        //对家弃牌区
        [JsonIgnore]
        public Point[] OppositeDiscardPondArea { get; set; } = new Point[4];

        //上家手牌+副露区
        [JsonIgnore]
        public Point[] LeftHandAndMeldArea { get; set; } = new Point[4];
        //上家弃牌区
        [JsonIgnore]
        public Point[] LeftDiscardPondArea { get; set; } = new Point[4];

        /// <summary>
        /// 根据 Ratio 坐标和参照矩形计算四个基础区域的绝对坐标，然后派生剩余区域。
        /// Ratio 全为 null 时返回 false 不修改任何坐标。
        /// </summary>
        /// <param name="referenceRect">参照矩形（游戏窗口客户区 或 屏幕全屏），屏幕坐标</param>
        /// <returns>参照矩形是否有效（宽>0 且 高>0），Ratio 全 null 返回 false</returns>
        public bool ResolveAbsoluteCoordsFromRatios(RectangleF referenceRect)
        {
            if (DoraIndicatorRatio == null || TableRatio == null ||
                DiscardPondRatio == null || InfoRatio == null)
                return false;

            if (referenceRect.Width <= 0 || referenceRect.Height <= 0)
            {
                // 无效参照矩形：fallback 到全零，派生区域也全零
                DoraIndicatorArea = new Point[4];
                TableArea = new Point[4];
                DiscardPondArea = new Point[4];
                InfoArea = new Point[4];
                ComputeDerivedAreas();
                return false;
            }

            DoraIndicatorArea = RatioToAbsolute(DoraIndicatorRatio, referenceRect);
            TableArea = RatioToAbsolute(TableRatio, referenceRect);
            DiscardPondArea = RatioToAbsolute(DiscardPondRatio, referenceRect);
            InfoArea = RatioToAbsolute(InfoRatio, referenceRect);
            ComputeDerivedAreas();
            return true;
        }

        /// <summary>
        /// 将一组 Ratio 点（0~1）转换为屏幕绝对坐标。
        /// </summary>
        private static Point[] RatioToAbsolute(PointF[] ratios, RectangleF refRect)
        {
            var abs = new Point[ratios.Length];
            for (int i = 0; i < ratios.Length; i++)
            {
                abs[i] = new Point(
                    (int)Math.Round(refRect.X + ratios[i].X * refRect.Width),
                    (int)Math.Round(refRect.Y + ratios[i].Y * refRect.Height));
            }
            return abs;
        }

        /// <summary>
        /// 使用四个基础区域（TableArea、DiscardPondArea、InfoArea）计算所有八个派生区域。
        /// 在 JSON 反序列化后、基础区域变更后、或 CopyFrom 后调用。
        /// </summary>
        public void ComputeDerivedAreas()
        {
            if (TableArea.Length != 4 || DiscardPondArea.Length != 4 || InfoArea.Length != 4)
                return;

            // 弃牌区：QuadB 与 QuadD 之间的梯形
            SelfDiscardPondArea = new[] { DiscardPondArea[3], InfoArea[3], InfoArea[2], DiscardPondArea[2] };
            RightDiscardPondArea = new[] { DiscardPondArea[2], InfoArea[2], InfoArea[1], DiscardPondArea[1] };
            OppositeDiscardPondArea = new[] { DiscardPondArea[1], InfoArea[1], InfoArea[0], DiscardPondArea[0] };
            LeftDiscardPondArea = new[] { DiscardPondArea[0], InfoArea[0], InfoArea[3], DiscardPondArea[3] };

            // 手牌+副露区：从 QuadB 各边向外延伸与 TableArea 边界求交
            var intersectA = GeometryHelper.FindRayBoundaryIntersection(DiscardPondArea, TableArea, 1, 0);
            var intersectB = GeometryHelper.FindRayBoundaryIntersection(DiscardPondArea, TableArea, 2, 1);
            var intersectC = GeometryHelper.FindRayBoundaryIntersection(DiscardPondArea, TableArea, 3, 2);
            var intersectD = GeometryHelper.FindRayBoundaryIntersection(DiscardPondArea, TableArea, 0, 3);

            LeftHandAndMeldArea = new[] { intersectA, DiscardPondArea[0], intersectD, TableArea[3] };
            OppositeHandAndMeldArea = new[] { intersectB, DiscardPondArea[1], intersectA, TableArea[0] };
            RightHandAndMeldArea = new[] { intersectC, DiscardPondArea[2], intersectB, TableArea[1] };
            SelfHandAndMeldArea = new[] { intersectD, DiscardPondArea[3], intersectC, TableArea[2] };
        }

        /// <summary>从另一实例复制基础配置值（用于 Reload 时原地更新单例）。</summary>
        public void CopyFrom(ScreenCaptureOptions other)
        {
            AdapterIndex = other.AdapterIndex;
            OutputIndex = other.OutputIndex;
            GameProcessName = other.GameProcessName;
            DoraIndicatorRatio = other.DoraIndicatorRatio;
            TableRatio = other.TableRatio;
            DiscardPondRatio = other.DiscardPondRatio;
            InfoRatio = other.InfoRatio;
            DoraIndicatorArea = other.DoraIndicatorArea;
            TableArea = other.TableArea;
            DiscardPondArea = other.DiscardPondArea;
            InfoArea = other.InfoArea;
            ComputeDerivedAreas();
        }
    }
}
