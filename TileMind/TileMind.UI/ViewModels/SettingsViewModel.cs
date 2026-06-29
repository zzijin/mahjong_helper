using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using TileMind.Common.Config;
using TileMind.Common.Helpers;

namespace TileMind.UI.ViewModels;

public partial class SettingsViewModel : ViewModel
{
    private bool _isInitialized;

    [ObservableProperty]
    private string _appVersion = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private Wpf.Ui.Appearance.ApplicationTheme _currentApplicationTheme = Wpf.Ui.Appearance.ApplicationTheme.Unknown;

    // ── 配置暴露 ──

    public OverlayOptions Overlay { get; }
    public PipelineOptions Pipeline { get; }
    public ScreenCaptureOptions ScreenCapture { get; }
    public YoloOptions Yolo { get; }
    public FrameFusionOptions Fusion { get; }
    public GameStateTrackerOptions Tracker { get; }

    public SettingsViewModel(
        IOptions<OverlayOptions> overlay,
        IOptions<PipelineOptions> pipeline,
        IOptions<ScreenCaptureOptions> screenCapture,
        IOptions<YoloOptions> yolo,
        IOptions<FrameFusionOptions> fusion,
        IOptions<GameStateTrackerOptions> tracker)
    {
        Overlay = overlay.Value;
        Pipeline = pipeline.Value;
        ScreenCapture = screenCapture.Value;
        Yolo = yolo.Value;
        Fusion = fusion.Value;
        Tracker = tracker.Value;
    }

    public override void OnNavigatedTo()
    {
        if (!_isInitialized)
            InitializeViewModel();
    }

    private void InitializeViewModel()
    {
        CurrentApplicationTheme = Wpf.Ui.Appearance.ApplicationThemeManager.GetAppTheme();
        AppVersion = $"TileMind - {GetAssemblyVersion()}";
        _isInitialized = true;
    }

    private static string GetAssemblyVersion()
        => System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty;

    [RelayCommand]
    private void SaveSettings()
    {
        try
        {
            Overlay.Save();
            Pipeline.Save();
            Yolo.Save();
            Fusion.Save();
            Tracker.Save();
            StatusMessage = "Settings saved successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OnChangeTheme(string parameter)
    {
        switch (parameter)
        {
            case "theme_light":
                if (CurrentApplicationTheme != Wpf.Ui.Appearance.ApplicationTheme.Light)
                {
                    Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Light);
                    CurrentApplicationTheme = Wpf.Ui.Appearance.ApplicationTheme.Light;
                }
                break;
            default:
                if (CurrentApplicationTheme != Wpf.Ui.Appearance.ApplicationTheme.Dark)
                {
                    Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Dark);
                    CurrentApplicationTheme = Wpf.Ui.Appearance.ApplicationTheme.Dark;
                }
                break;
        }
    }
}
