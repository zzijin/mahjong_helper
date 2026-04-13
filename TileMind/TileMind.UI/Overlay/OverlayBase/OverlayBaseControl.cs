using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text;
using System.Windows;
using System.Windows.Media;
using TileMind.UI.Overlay.OverlayBase.DrawingCommand;

namespace TileMind.UI.Overlay.OverlayBase
{
    public abstract class OverlayBaseControl : UIElement
    {
        private readonly VisualCollection _visuals;
        private readonly Dictionary<DrawingInfo, DrawingVisual> _visualMap = new();
        private MatrixTransform _renderTransform = new(Matrix.Identity);

        // 依赖属性：数据源集合
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(ObservableCollection<DrawingInfo>), typeof(OverlayBaseControl),
                new FrameworkPropertyMetadata(null, OnItemsSourceChanged));

        public ObservableCollection<DrawingInfo> ItemsSource
        {
            get => (ObservableCollection<DrawingInfo>)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        // 全局样式属性（可被子类重写默认值）
        public static readonly DependencyProperty FillOpacityProperty =
            DependencyProperty.Register(nameof(FillOpacity), typeof(double), typeof(OverlayBaseControl),
                new FrameworkPropertyMetadata(0.3, FrameworkPropertyMetadataOptions.AffectsRender));

        public double FillOpacity
        {
            get => (double)GetValue(FillOpacityProperty);
            set => SetValue(FillOpacityProperty, value);
        }

        public static readonly DependencyProperty StrokeThicknessProperty =
            DependencyProperty.Register(nameof(StrokeThickness), typeof(double), typeof(OverlayBaseControl),
                new FrameworkPropertyMetadata(2.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double StrokeThickness
        {
            get => (double)GetValue(StrokeThicknessProperty);
            set => SetValue(StrokeThicknessProperty, value);
        }

        protected OverlayBaseControl()
        {
            _visuals = new VisualCollection(this);
            //Background = Brushes.Transparent;
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var overlay = (OverlayBaseControl)d;
            if (e.OldValue is ObservableCollection<DrawingInfo> oldCol)
            {
                oldCol.CollectionChanged -= overlay.OnCollectionChanged;
                foreach (var item in oldCol)
                    overlay.RemoveVisualForItem(item);
            }
            if (e.NewValue is ObservableCollection<DrawingInfo> newCol)
            {
                newCol.CollectionChanged += overlay.OnCollectionChanged;
                foreach (var item in newCol)
                    overlay.AddVisualForItem(item);
            }
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (DrawingInfo item in e.OldItems)
                    RemoveVisualForItem(item);
            if (e.NewItems != null)
                foreach (DrawingInfo item in e.NewItems)
                    AddVisualForItem(item);
        }

        private void AddVisualForItem(DrawingInfo item)
        {
            var visual = new DrawingVisual();
            _visualMap[item] = visual;
            _visuals.Add(visual);
            RenderItem(item);
        }

        private void RemoveVisualForItem(DrawingInfo item)
        {
            if (_visualMap.TryGetValue(item, out var visual))
            {
                _visuals.Remove(visual);
                _visualMap.Remove(item);
            }
        }

        /// <summary>
        /// 局部更新指定数据项（性能优化的核心入口）
        /// </summary>
        public void UpdateItemVisual(DrawingInfo item)
        {
            if (_visualMap.TryGetValue(item, out _))
                RenderItem(item);
        }

        /// <summary>
        /// 刷新所有数据项的绘制
        /// </summary>
        public void RefreshAll()
        {
            foreach (var item in _visualMap.Keys)
                RenderItem(item);
        }

        /// <summary>
        /// 设置从原始坐标系到控件坐标系的变换矩阵
        /// </summary>
        public void SetRenderTransform(Matrix matrix)
        {
            _renderTransform = new MatrixTransform(matrix);
            RefreshAll();
        }

        /// <summary>
        /// 抽象方法：由子类实现如何将一个数据项转换为绘制命令集合
        /// </summary>
        protected abstract IEnumerable<IDrawingCommand> GenerateCommandsForItem(DrawingInfo item);

        /// <summary>
        /// 抽象方法：由子类提供绘制时使用的填充画刷和描边画笔（可根据数据项动态决定）
        /// </summary>
        protected abstract (Brush fillBrush, Pen strokePen) GetDrawingStyles(DrawingInfo item);

        private void RenderItem(DrawingInfo item)
        {
            if (!_visualMap.TryGetValue(item, out var visual))
                return;

            var commands = GenerateCommandsForItem(item);
            var (fillBrush, strokePen) = GetDrawingStyles(item);

            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.PushTransform(_renderTransform);
                foreach (var cmd in commands)
                {
                    cmd.Draw(dc, fillBrush, strokePen);
                }
                dc.Pop();
            }
        }
    }
}
