using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;
using TileMind.Common.Logging;
using TileMind.Core.Services;
using TileMind.UI.Services;
using TileMind.UI.Views;
using TileMind.UI.ViewModels;
using TileMind.Vision.Detection;
using TileMind.Vision.ScreenCapture;
using Wpf.Ui;
using Wpf.Ui.DependencyInjection;

namespace TileMind.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private IServiceProvider? _serviceProvider;
        private static readonly IHost _host = Host.CreateDefaultBuilder()
            .ConfigureServices(
                (context, services) =>
                {
                    ConfigureServices(services);
                }
            )
        .Build();

        private static void ConfigureServices(IServiceCollection services)
        {
            //注册托管服务
            services.AddHostedService<ApplicationHostService>();

            services.AddBaseConfig();
            services.AddBaseServices();

            //注册UI服务

            //注册导航服务
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<INavigationWindow, MainWindow>();
            services.AddNavigationViewPageProvider();

            //注册窗口和页面
            //services.AddSingleton<MainWindow>();
            services.AddSingleton<MainWindowViewModel>();
            services.AddSingleton<HomePage>();
            services.AddSingleton<HomeViewModel>();
            services.AddSingleton<SettingsPage>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<OverlayWindow>();
            services.AddSingleton<OverlayWindowViewModel>();
            services.AddSingleton<ScreenSplitterOverlayControl>();
            services.AddSingleton<ScreenSplitterViewModel>();
            services.AddTransient<ScreenSplitterWindow>();

        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // 启动主机
            await _host.StartAsync();
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
            // 停止主机
            if (_host != null)
            {
                await _host.StopAsync();
                _host.Dispose();
            }
        }
    }
}
