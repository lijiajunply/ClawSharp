using System.CommandLine;
using ClawSharp.CLI.Infrastructure;
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Hub;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;

namespace ClawSharp.CLI.Commands;

public static class HubCommands
{
    public static Command Create(IHost host)
    {
        var command = new Command("hub", "Browse and install skills from ClawHub");
        command.AddCommand(CreateSearch(host));
        command.AddCommand(CreateShow(host));
        command.AddCommand(CreateInstall(host));
        return command;
    }

    private static Command CreateSearch(IHost host)
    {
        var command = new Command("search", "Search skills in ClawHub");
        var queryArg = new Argument<string?>("query", () => null, "Optional search query");
        command.AddArgument(queryArg);

        command.SetHandler(async query =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                await EnsureHubReadyAsync(host);
                var client = host.Services.GetRequiredService<IHubClient>();
                var results = await client.SearchSkillsAsync(query);

                if (results.Count == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]No skills found in ClawHub.[/]");
                    return 0;
                }

                var table = new Table().Border(TableBorder.Rounded).Expand();
                table.AddColumn("ID");
                table.AddColumn("Name");
                table.AddColumn("Latest Version");
                table.AddColumn("Description");

                foreach (var skill in results)
                {
                    table.AddRow(
                        skill.Id.EscapeMarkup(),
                        skill.Name.EscapeMarkup(),
                        skill.LatestVersion.EscapeMarkup(),
                        skill.Description.EscapeMarkup());
                }

                AnsiConsole.Write(table);
                return 0;
            });
        }, queryArg);

        return command;
    }

    private static Command CreateShow(IHost host)
    {
        var command = new Command("show", "Show detailed information for a ClawHub skill");
        var skillIdArg = new Argument<string>("skill-id", "The skill id to inspect");
        command.AddArgument(skillIdArg);

        command.SetHandler(async skillId =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                await EnsureHubReadyAsync(host);
                var client = host.Services.GetRequiredService<IHubClient>();
                var skill = await client.GetSkillAsync(skillId);

                var panelContent =
                    $"[bold]{skill.Name.EscapeMarkup()}[/] ([blue]{skill.Id.EscapeMarkup()}[/]){Environment.NewLine}" +
                    $"Latest: [green]{skill.LatestVersion.EscapeMarkup()}[/]{Environment.NewLine}" +
                    $"Downloads: {skill.Downloads}{Environment.NewLine}" +
                    $"Versions: {(skill.Versions.Count == 0 ? "[grey]n/a[/]" : string.Join(", ", skill.Versions.Select(v => v.EscapeMarkup())))}{Environment.NewLine}" +
                    $"Tags: {(skill.Tags.Count == 0 ? "[grey]none[/]" : string.Join(", ", skill.Tags.Select(t => t.EscapeMarkup())))}{Environment.NewLine}{Environment.NewLine}" +
                    $"{skill.Description.EscapeMarkup()}";

                AnsiConsole.Write(new Panel(new Markup(panelContent))
                {
                    Header = new PanelHeader("ClawHub Skill"),
                    Border = BoxBorder.Rounded
                });

                if (!string.IsNullOrWhiteSpace(skill.Readme))
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[bold]README[/]");
                    AnsiConsole.WriteLine(skill.Readme);
                }

                return 0;
            });
        }, skillIdArg);

        return command;
    }

    private static Command CreateInstall(IHost host)
    {
        var command = new Command("install", "Download and install a ClawHub skill");
        var skillIdArg = new Argument<string>("skill-id", "The skill id to install");
        var versionOption = new Option<string?>("--version", "Install a specific version");
        var forceOption = new Option<bool>("--force", "Replace an existing installation");
        command.AddArgument(skillIdArg);
        command.AddOption(versionOption);
        command.AddOption(forceOption);

        command.SetHandler(async (skillId, version, force) =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                await EnsureHubReadyAsync(host);
                var client = host.Services.GetRequiredService<IHubClient>();
                var installer = host.Services.GetRequiredService<IHubInstaller>();
                var detail = await client.GetSkillAsync(skillId);
                var resolvedVersion = string.IsNullOrWhiteSpace(version) ? detail.LatestVersion : version;
                var package = await client.DownloadSkillPackageAsync(skillId, resolvedVersion!);
                var installed = await installer.InstallAsync(package, InstallTarget.UserHome, force);

                AnsiConsole.MarkupLine($"[green]Installed[/] [blue]{installed.SkillId.EscapeMarkup()}[/] [grey]v{installed.Version.EscapeMarkup()}[/]");
                AnsiConsole.MarkupLine($"Path: [grey]{installed.InstallPath.EscapeMarkup()}[/]");
                AnsiConsole.MarkupLine($"Registry ID: [grey]user.{installed.Definition.Id.EscapeMarkup()}[/]");
                return 0;
            });
        }, skillIdArg, versionOption, forceOption);

        return command;
    }

    private static Task EnsureHubReadyAsync(IHost host)
    {
        var options = host.Services.GetRequiredService<ClawOptions>();
        if (!options.Hub.Enabled)
        {
            throw new InvalidOperationException("ClawHub integration is disabled. Set Hub:Enabled=true to use hub commands.");
        }

        if (string.IsNullOrWhiteSpace(options.Hub.BaseUrl))
        {
            throw new InvalidOperationException("Hub:BaseUrl is required before using hub commands.");
        }

        return Task.CompletedTask;
    }
}
