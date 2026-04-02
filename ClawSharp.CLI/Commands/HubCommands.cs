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
        var command = new Command("hub", I18n.T("Hub.Description"));
        command.AddCommand(CreateSearch(host));
        command.AddCommand(CreateShow(host));
        command.AddCommand(CreateInstall(host));
        return command;
    }

    private static Command CreateSearch(IHost host)
    {
        var command = new Command("search", I18n.T("Hub.Search.Description"));
        var queryArg = new Argument<string?>("query", () => null, I18n.T("Hub.Search.QueryArg"));
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
                    AnsiConsole.MarkupLine(I18n.T("Hub.Search.Empty"));
                    return 0;
                }

                var table = new Table().Border(TableBorder.Rounded).Expand();
                table.AddColumn(I18n.T("Hub.Search.Column.ID"));
                table.AddColumn(I18n.T("Hub.Search.Column.Name"));
                table.AddColumn(I18n.T("Hub.Search.Column.LatestVersion"));
                table.AddColumn(I18n.T("Hub.Search.Column.Description"));

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
        var command = new Command("show", I18n.T("Hub.Show.Description"));
        var skillIdArg = new Argument<string>("skill-id", I18n.T("Hub.Show.SkillArg"));
        command.AddArgument(skillIdArg);

        command.SetHandler(async skillId =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                await EnsureHubReadyAsync(host);
                var client = host.Services.GetRequiredService<IHubClient>();
                var skill = await client.GetSkillAsync(skillId);

                var panelContent = I18n.T(
                    "Hub.Show.Summary",
                    skill.Name.EscapeMarkup(),
                    skill.Id.EscapeMarkup(),
                    Environment.NewLine,
                    skill.LatestVersion.EscapeMarkup(),
                    skill.Downloads.ToString(),
                    skill.Versions.Count == 0 ? $"[grey]{I18n.T("Common.NotApplicable")}[/]" : string.Join(", ", skill.Versions.Select(v => v.EscapeMarkup())),
                    skill.Tags.Count == 0 ? $"[grey]{I18n.T("Common.None")}[/]" : string.Join(", ", skill.Tags.Select(t => t.EscapeMarkup())),
                    skill.Description.EscapeMarkup());

                AnsiConsole.Write(new Panel(new Markup(panelContent))
                {
                    Header = new PanelHeader(I18n.T("Hub.Show.Panel")),
                    Border = BoxBorder.Rounded
                });

                if (!string.IsNullOrWhiteSpace(skill.Readme))
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"[bold]{I18n.T("Common.Readme")}[/]");
                    AnsiConsole.WriteLine(skill.Readme);
                }

                return 0;
            });
        }, skillIdArg);

        return command;
    }

    private static Command CreateInstall(IHost host)
    {
        var command = new Command("install", I18n.T("Hub.Install.Description"));
        var skillIdArg = new Argument<string>("skill-id", I18n.T("Hub.Install.SkillArg"));
        var versionOption = new Option<string?>("--version", I18n.T("Hub.Install.VersionOption"));
        var forceOption = new Option<bool>("--force", I18n.T("Hub.Install.ForceOption"));
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

                AnsiConsole.MarkupLine(I18n.T("Hub.Install.Success", installed.SkillId.EscapeMarkup(), installed.Version.EscapeMarkup()));
                AnsiConsole.MarkupLine(I18n.T("Hub.Install.Path", installed.InstallPath.EscapeMarkup()));
                AnsiConsole.MarkupLine(I18n.T("Hub.Install.RegistryId", installed.Definition.Id.EscapeMarkup()));
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
            throw new InvalidOperationException(I18n.T("Hub.Disabled"));
        }

        if (string.IsNullOrWhiteSpace(options.Hub.BaseUrl))
        {
            throw new InvalidOperationException(I18n.T("Hub.BaseUrlRequired"));
        }

        return Task.CompletedTask;
    }
}
