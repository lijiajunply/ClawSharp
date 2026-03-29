using ClawSharp.Lib.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClawSharp.CLI.Infrastructure;

public static class ServiceConfigurator
{
    public static IHost BuildHost(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging =>
            {
                // 屏蔽 EF Core 的信息级别日志，只保留警告和错误
                logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
                logging.AddFilter("Microsoft.EntityFrameworkCore.Migrations", LogLevel.Warning);
                logging.AddFilter("Microsoft.EntityFrameworkCore.Infrastructure", LogLevel.Warning);
            })
            .ConfigureServices((hostContext, services) =>
            {
                services.AddClawSharp(builder =>
                {
                    builder.BasePath = Directory.GetCurrentDirectory();
                });
            })
            .Build();
    }
}
