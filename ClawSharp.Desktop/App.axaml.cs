using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using ClawSharp.Desktop.ViewModels;
using ClawSharp.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;
using Avalonia.ReactiveUI;
using System.IO;
using System;

namespace ClawSharp.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            if (!File.Exists("appsettings.json") && !File.Exists("appsettings.Local.json"))
            {
                var bootstrapViewModel = new BootstrapViewModel();
                var bootstrapWindow = new BootstrapWindow
                {
                    DataContext = bootstrapViewModel,
                    ViewModel = bootstrapViewModel
                };

                bootstrapWindow.Opened += async (s, e) =>
                {
                    await bootstrapViewModel.InitializeAsync();
                };

                bootstrapWindow.Closed += (s, e) =>
                {
                    // User closed the window or finished bootstrap
                    if (!File.Exists("appsettings.json") && !File.Exists("appsettings.Local.json"))
                    {
                        Environment.Exit(0);
                        return;
                    }

                    // Reload configuration to ensure changes are picked up?
                    // We might need to restart IHost, but let's just proceed to MainWindow for now.
                    // Ideal flow: IHost is rebuilt if config changed, but for simplicity:
                    ShowMainWindow(desktop);
                };

                desktop.MainWindow = bootstrapWindow;
            }
            else
            {
                ShowMainWindow(desktop);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ShowMainWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var mainWindowViewModel = Infrastructure.ServiceConfigurator.ServiceProvider.GetRequiredService<MainWindowViewModel>();
        var mainWindow = new MainWindow
        {
            DataContext = mainWindowViewModel
        };

        if (desktop.MainWindow is BootstrapWindow currentWindow)
        {
            // The bootstrap window is currently the MainWindow
            desktop.MainWindow = mainWindow;
            mainWindow.Show();
        }
        else
        {
            desktop.MainWindow = mainWindow;
        }
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}