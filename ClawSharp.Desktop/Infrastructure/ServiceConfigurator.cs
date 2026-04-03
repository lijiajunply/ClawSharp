using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ClawSharp.Lib.Configuration;
using ClawSharp.Desktop.ViewModels;
using System.IO;
using System;

namespace ClawSharp.Desktop.Infrastructure;

public static class ServiceConfigurator
{
    public static IHost? AppHost { get; private set; }

    public static IServiceProvider ServiceProvider => AppHost?.Services ?? throw new InvalidOperationException("AppHost has not been initialized.");

    public static IHost BuildHost(string[] args)
    {
        AppHost = Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging =>
            {
                // 屏蔽 EF Core 的信息级别日志，只保留警告和错误
                logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
                logging.AddFilter("Microsoft.EntityFrameworkCore.Migrations", LogLevel.Warning);
                logging.AddFilter("Microsoft.EntityFrameworkCore.Infrastructure", LogLevel.Warning);
            })
            .ConfigureServices((hostContext, services) =>
            {
                // ClawSharp Core
                services.AddClawSharp(builder =>
                {
                    // 在开发环境下，如果 bin 目录下没有 workspace，尝试寻找源码根目录
                    var baseDir = Directory.GetCurrentDirectory();
                    if (!Directory.Exists(Path.Combine(baseDir, "workspace")) && 
                        !Directory.Exists(Path.Combine(baseDir, ".clawsharp")))
                    {
                        var parent = Directory.GetParent(baseDir);
                        while (parent != null)
                        {
                            if (Directory.Exists(Path.Combine(parent.FullName, "workspace")))
                            {
                                baseDir = parent.FullName;
                                break;
                            }
                            parent = parent.Parent;
                        }
                    }
                    builder.BasePath = baseDir;
                });

                // Register DesktopPermissionUI
                services.AddSingleton<Lib.Runtime.IPermissionUI, DesktopPermissionUI>();

                // ViewModels
                services.AddTransient<ChatViewModel>();
                services.AddTransient<AgentViewModel>();
                services.AddSingleton<HistoryViewModel>();
                services.AddSingleton<McpViewModel>();
                services.AddSingleton<ConfigViewModel>();
                services.AddSingleton<MainWindowViewModel>();
            })
            .Build();

        return AppHost;
    }
}
