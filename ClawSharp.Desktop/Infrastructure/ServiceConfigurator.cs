using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.IO;
using ClawSharp.Lib.Configuration;
using ClawSharp.Desktop.ViewModels;
using System;

namespace ClawSharp.Desktop.Infrastructure;

public static class ServiceConfigurator
{
    private static IServiceProvider? _serviceProvider;

    public static IServiceProvider ServiceProvider => _serviceProvider ?? throw new InvalidOperationException("Service provider has not been initialized.");

    public static void Configure()
    {
        var services = new ServiceCollection();

        // ClawSharp Core
        services.AddClawSharp(builder =>
        {
            builder.BasePath = Directory.GetCurrentDirectory();
        });

        // ViewModels
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<ChatViewModel>();
        services.AddTransient<AgentViewModel>();

        _serviceProvider = services.BuildServiceProvider();
    }
}
