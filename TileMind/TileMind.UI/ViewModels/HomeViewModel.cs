using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using TileMind.UI.Views;

namespace TileMind.UI.ViewModels;

public partial class HomeViewModel : ViewModel
{
    private readonly IServiceProvider _serviceProvider;

    public HomeViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    [RelayCommand]
    private void OpenScreenSplitter()
    {
        var mainWindow = System.Windows.Application.Current.MainWindow;
        var window = _serviceProvider.GetRequiredService<ScreenSplitterWindow>();
        window.Owner = mainWindow;

        mainWindow.WindowState = System.Windows.WindowState.Minimized;
        window.ShowDialog();
        mainWindow.WindowState = System.Windows.WindowState.Normal;
    }
}
