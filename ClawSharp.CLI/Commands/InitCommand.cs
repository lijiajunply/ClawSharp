using System.CommandLine;
using ClawSharp.CLI.Infrastructure;
using ClawSharp.Lib.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;

namespace ClawSharp.CLI.Commands;

public static class InitCommand
{
    public static Command Create(IHost host)
    {
        var command = new Command("init", "Initialize a ThreadSpace in the current folder");
        var pathOption = new Option<string>("--path", "The directory to initialize");
        pathOption.SetDefaultValue(".");
        command.AddOption(pathOption);

        command.SetHandler(async path =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                var runtime = host.Services.GetRequiredService<IClawRuntime>();
                
                var absolutePath = Path.GetFullPath(path);
                AnsiConsole.MarkupLine($"[bold blue]Initializing ThreadSpace at:[/] [green]{absolutePath.EscapeMarkup()}[/]");

                await AnsiConsole.Status()
                    .StartAsync("Working...", async ctx =>
                    {
                        await runtime.InitializeAsync();
                    });

                AnsiConsole.MarkupLine("[bold green]Success![/] ThreadSpace initialized.");
                return 0;
            });
        }, pathOption);

        return command;
    }
}
