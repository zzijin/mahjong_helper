using TileMind.Common.Config;
using TileMind.Common.Helpers;
using TileMind.UI.Views;

namespace TileMind.UI.ViewModels;

public class ScreenSplitterViewModel : ViewModel
{
    private readonly ScreenCaptureOptions _options;

    public ScreenSplitterViewModel(ScreenCaptureOptions options)
    {
        _options = options;
    }

    public void LoadConfig(ScreenSplitterOverlayControl control)
    {
        control.LoadFromOptions(_options);
    }

    public void SaveConfig(ScreenSplitterOverlayControl control)
    {
        control.WriteToOptions(_options);
        _options.Save();
    }

    public void ReloadConfig(ScreenSplitterOverlayControl control)
    {
        var loaded = SettingConfigExtensions.Load<ScreenCaptureOptions>(
            ScreenCaptureOptions.SettingFilePath);
        if (loaded != null)
        {
            _options.CopyFrom(loaded);
        }
        LoadConfig(control);
    }
}
