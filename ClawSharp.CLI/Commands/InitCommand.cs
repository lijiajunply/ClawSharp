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
        var rootCommand = new Command("init", "Initialization commands");

        rootCommand.AddCommand(CreateThreadSpaceCommand(host));
        rootCommand.AddCommand(CreateProjectCommand(host));

        return rootCommand;
    }

    private static Command CreateThreadSpaceCommand(IHost host)
    {
        var command = new Command("space", "Initialize a ThreadSpace in the current folder (default)");
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

                AnsiConsole.MarkupLine("[bold green]Success![/] ThreadSpace infrastructure initialized.");
                return 0;
            });
        }, pathOption);

        return command;
    }

    private static Command CreateProjectCommand(IHost host)
    {
        var command = new Command("proj", "Create a new project from a template with AI assistance");
        var nameArgument = new Argument<string?>("name", () => null, "The name of the project");
        var templateOption = new Option<string?>("--template", "The template ID to use");
        var pathOption = new Option<string>("--path", "The target directory");
        pathOption.SetDefaultValue(".");

        command.AddArgument(nameArgument);
        command.AddOption(templateOption);
        command.AddOption(pathOption);

        command.SetHandler(async (name, templateId, path) =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                await ProjectInitHandler.RunAsync(host, templateId, name, path);
                return 0;
            });
        }, nameArgument, templateOption, pathOption);

        return command;
    }
}
