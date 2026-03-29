using System.CommandLine;
using ClawSharp.CLI.Infrastructure;
using ClawSharp.Lib.Agents;
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Providers;
using ClawSharp.Lib.Runtime;
using ClawSharp.Lib.Skills;
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

                var kernel = host.Services.GetRequiredService<IClawKernel>();
                var options = host.Services.GetRequiredService<ClawOptions>();
                
                AnsiConsole.MarkupLine($"[grey]Config BasePath: {Directory.GetCurrentDirectory().EscapeMarkup()}[/]");
                AnsiConsole.MarkupLine($"[grey]Default Provider: {options.Providers.DefaultProvider.EscapeMarkup()}[/]");
                AnsiConsole.MarkupLine($"[grey]Model Count: {options.Providers.Models.Count}[/]");

                var agents = kernel.Agents.GetAll();

                var table = new Table();
                table.AddColumn("ID");
                table.AddColumn("Name");
                table.AddColumn("Configured Provider");
                table.AddColumn("Resolved Provider");
                table.AddColumn("Resolved Model");
                table.AddColumn("Version");

                var resolver = host.Services.GetRequiredService<IModelProviderResolver>();

                foreach (var agent in agents)
                {
                    string resolvedProvider = "Error";
                    string resolvedModel = "Error";
                    try 
                    {
                        var resolved = resolver.Resolve(agent);
                        resolvedProvider = resolved.Target.ProviderName;
                        resolvedModel = resolved.Target.Model;
                    }
                    catch (Exception ex)
                    {
                        resolvedProvider = $"[red]Error: {ex.Message.EscapeMarkup()}[/]";
                    }

                    table.AddRow(
                        agent.Id.EscapeMarkup(),
                        agent.Name.EscapeMarkup(),
                        (agent.Provider ?? "[grey]default[/]").EscapeMarkup(),
                        resolvedProvider.EscapeMarkup(),
                        resolvedModel.EscapeMarkup(),
                        agent.Version.EscapeMarkup());
                }

                AnsiConsole.Write(table);
                return 0;
            });
        });
        return command;
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
                table.AddColumn("Name");
                table.AddColumn("Version");

                foreach (var skill in skills)
                {
                    table.AddRow(
                        skill.Id,
                        skill.Name,
                        skill.Version);
                }

                AnsiConsole.Write(table);
                return 0;
            });
        });
        return command;
    }
}
