using System.Windows;
using TileMind.UI.ViewModels;

namespace TileMind.UI.Views;

public partial class ScreenSplitterWindow : Window
{
    private readonly ScreenSplitterOverlayControl _splitterControl;
    private readonly ScreenSplitterViewModel _viewModel;

    public ScreenSplitterWindow(ScreenSplitterOverlayControl splitterControl, ScreenSplitterViewModel viewModel)
    {
        _splitterControl = splitterControl;
        _viewModel = viewModel;
        InitializeComponent();

        // 单例控件可能还挂在上一个窗口上，先断开再插入
        if (_splitterControl.Parent is System.Windows.Controls.Panel parent)
            parent.Children.Remove(_splitterControl);
        RootGrid.Children.Insert(0, _splitterControl);
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel.LoadConfig(_splitterControl);
    }

    private void LoadButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ReloadConfig(_splitterControl);
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SaveConfig(_splitterControl);
        MessageBox.Show("区域配置已保存。", "TileMind", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        RootGrid.Children.Remove(_splitterControl);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
