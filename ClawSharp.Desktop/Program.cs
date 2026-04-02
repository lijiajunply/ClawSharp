using Avalonia;
using Avalonia.ReactiveUI;
using System;
using Microsoft.Extensions.Hosting;

namespace ClawSharp.Desktop;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        Infrastructure.I18n.InitializeForBootstrap();
        
        var host = Infrastructure.ServiceConfigurator.BuildHost(args);
        Infrastructure.I18n.Initialize(host);
        
        try
        {
            host.Start();
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            host.StopAsync().GetAwaiter().GetResult();
            host.Dispose();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}
