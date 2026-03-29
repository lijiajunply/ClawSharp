using ClawSharp.Lib.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ClawSharp.CLI.Infrastructure;

public static class ServiceConfigurator
{
    public static IHost BuildHost(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
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
