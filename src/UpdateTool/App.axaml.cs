using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using UpdateTool.ViewModels;
using UpdateTool.Views;

namespace UpdateTool
{
    public partial class App : Application
    {
        private static IHost? host;
        public static IServiceProvider Services => host?.Services ?? throw new InvalidOperationException("Host not initialized");
        public static IConfiguration Configuration => Services.GetRequiredService<IConfiguration>();
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            host = HostBuilder.Create().Build();
            Core.App.Init(Services);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var vm = Services.GetRequiredService<MainWindowViewModel>();

                desktop.MainWindow = new MainWindow
                {
                    DataContext = vm
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}