using System.CommandLine;
using ClawSharp.CLI.Infrastructure;
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Providers;
using ClawSharp.Lib.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;

namespace ClawSharp.CLI.Commands;

public static class RegistryCommands
{
    public static Command CreateAgents(IHost host)
    {
        var command = new Command("agents", I18n.T("Registry.Agents.Description"));
        command.SetHandler(async () =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                var runtime = host.Services.GetRequiredService<IClawRuntime>();
                await runtime.InitializeAsync();

                var options = host.Services.GetRequiredService<ClawOptions>();
                AnsiConsole.MarkupLine(I18n.T("Registry.Agents.ConfigBasePath", Directory.GetCurrentDirectory().EscapeMarkup()));
                AnsiConsole.MarkupLine(I18n.T("Registry.Agents.DefaultProvider", options.Providers.DefaultProvider.EscapeMarkup()));
                AnsiConsole.MarkupLine(I18n.T("Registry.Agents.ModelCount", options.Providers.Models.Count));

                await RenderAgentsAsync(host.Services);
                return 0;
            });
        });
        return command;
    }

    public static async Task RenderAgentsAsync(IServiceProvider services)
    {
        var kernel = services.GetRequiredService<IClawKernel>();
        var resolver = services.GetRequiredService<IModelProviderResolver>();

        var agents = kernel.Agents.GetAll()
            .OrderBy(a => a.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (agents.Length == 0)
        {
            AnsiConsole.MarkupLine(I18n.T("Registry.Agents.Empty"));
            return;
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn($"[yellow]{I18n.T("Common.ID")}[/]");
        table.AddColumn($"[yellow]{I18n.T("Common.Name")}[/]");
        table.AddColumn($"[yellow]{I18n.T("Chat.Skills.Column.Source")}[/]");
        table.AddColumn($"[yellow]{I18n.T("Registry.Agents.Column.ConfiguredProvider")}[/]");
        table.AddColumn($"[yellow]{I18n.T("Registry.Agents.Column.ResolvedProvider")}[/]");
        table.AddColumn($"[yellow]{I18n.T("Registry.Agents.Column.ResolvedModel")}[/]");
        table.AddColumn($"[yellow]{I18n.T("Common.Version")}[/]");

        foreach (var agent in agents)
        {
            string resolvedProvider;
            string resolvedModel;
            try
            {
                var resolved = resolver.Resolve(agent);
                resolvedProvider = resolved.Target.ProviderName;
                resolvedModel = resolved.Target.Model;
            }
            catch (Exception ex)
            {
                resolvedProvider = I18n.T("Registry.Agents.ResolveError", ex.Message);
                resolvedModel = string.Empty;
            }

            table.AddRow(
                agent.Id.EscapeMarkup(),
                agent.Name.EscapeMarkup(),
                agent.Source.ToString().EscapeMarkup(),
                (agent.Provider ?? I18n.T("Registry.Agents.DefaultProviderValue")).EscapeMarkup(),
                resolvedProvider.EscapeMarkup(),
                resolvedModel.EscapeMarkup(),
                agent.Version.EscapeMarkup());
        }

        AnsiConsole.Write(table);
    }

    public static Command CreateSkills(IHost host)
    {
        var command = new Command("skills", I18n.T("Registry.Skills.Description"));
        command.SetHandler(async () =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                var runtime = host.Services.GetRequiredService<IClawRuntime>();
                await runtime.InitializeAsync();

                var kernel = host.Services.GetRequiredService<IClawKernel>();
                var skills = kernel.Skills.GetAll();

                var table = new Table();
                table.AddColumn(I18n.T("Common.ID"));
                table.AddColumn(I18n.T("Chat.Skills.Column.Source"));
                table.AddColumn(I18n.T("Common.Name"));
                table.AddColumn(I18n.T("Common.Version"));

                foreach (var skill in skills)
                {
                    table.AddRow(
                        skill.Id.EscapeMarkup(),
                        skill.Source.ToString(),
                        skill.Name.EscapeMarkup(),
                        skill.Version.EscapeMarkup());
                }

                AnsiConsole.Write(table);
                return 0;
            });
        });
        return command;
    }
}
