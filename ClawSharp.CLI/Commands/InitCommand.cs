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
        var rootCommand = new Command("init", I18n.T("Init.Description"));

        rootCommand.AddCommand(CreateThreadSpaceCommand(host));
        rootCommand.AddCommand(CreateProjectCommand(host));

        return rootCommand;
    }

    private static Command CreateThreadSpaceCommand(IHost host)
    {
        var command = new Command("space", I18n.T("Init.Space.Description"));
        var pathOption = new Option<string>("--path", I18n.T("Init.Space.PathOption"));
        pathOption.SetDefaultValue(".");
        command.AddOption(pathOption);

        command.SetHandler(async path =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                var runtime = host.Services.GetRequiredService<IClawRuntime>();
                
                var absolutePath = Path.GetFullPath(path);
                AnsiConsole.MarkupLine(I18n.T("Init.Space.Start", absolutePath.EscapeMarkup()));

                await AnsiConsole.Status()
                    .StartAsync(I18n.T("Init.Space.Working"), async ctx =>
                    {
                        await runtime.InitializeAsync();
                    });

                AnsiConsole.MarkupLine(I18n.T("Init.Space.Success"));
                return 0;
            });
        }, pathOption);

        return command;
    }

    private static Command CreateProjectCommand(IHost host)
    {
        var command = new Command("proj", I18n.T("Init.Project.Description"));
        var nameArgument = new Argument<string?>("name", () => null, I18n.T("Init.Project.NameArg"));
        var templateOption = new Option<string?>("--template", I18n.T("Init.Project.TemplateOption"));
        var pathOption = new Option<string>("--path", I18n.T("Init.Project.PathOption"));
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
