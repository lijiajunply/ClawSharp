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
        var command = new Command("agents", "List all registered agents");
        command.SetHandler(async () =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                var runtime = host.Services.GetRequiredService<IClawRuntime>();
                await runtime.InitializeAsync();

                var options = host.Services.GetRequiredService<ClawOptions>();
                AnsiConsole.MarkupLine($"[grey]Config BasePath: {Directory.GetCurrentDirectory().EscapeMarkup()}[/]");
                AnsiConsole.MarkupLine($"[grey]Default Provider: {options.Providers.DefaultProvider.EscapeMarkup()}[/]");
                AnsiConsole.MarkupLine($"[grey]Model Count: {options.Providers.Models.Count}[/]");

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
            AnsiConsole.MarkupLine("[yellow]No registered agents were found.[/]");
            return;
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("[yellow]ID[/]");
        table.AddColumn("[yellow]Name[/]");
        table.AddColumn("[yellow]Source[/]");
        table.AddColumn("[yellow]Configured Provider[/]");
        table.AddColumn("[yellow]Resolved Provider[/]");
        table.AddColumn("[yellow]Resolved Model[/]");
        table.AddColumn("[yellow]Version[/]");

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
                resolvedProvider = $"[red]Error: {ex.Message.EscapeMarkup()}[/]";
                resolvedModel = string.Empty;
            }

            table.AddRow(
                agent.Id.EscapeMarkup(),
                agent.Name.EscapeMarkup(),
                agent.Source.ToString().EscapeMarkup(),
                (agent.Provider ?? "[grey]default[/]").EscapeMarkup(),
                resolvedProvider.EscapeMarkup(),
                resolvedModel.EscapeMarkup(),
                agent.Version.EscapeMarkup());
        }

        AnsiConsole.Write(table);
    }

    public static Command CreateSkills(IHost host)
    {
        var command = new Command("skills", "List all registered skills");
        command.SetHandler(async () =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                var runtime = host.Services.GetRequiredService<IClawRuntime>();
                await runtime.InitializeAsync();

                var kernel = host.Services.GetRequiredService<IClawKernel>();
                var skills = kernel.Skills.GetAll();

                var table = new Table();
                table.AddColumn("ID");
                table.AddColumn("Source");
                table.AddColumn("Name");
                table.AddColumn("Version");

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
