using System.CommandLine;
using ClawSharp.CLI.Infrastructure;
using ClawSharp.Lib.Agents;
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
                var agents = kernel.Agents.GetAll();

                var table = new Table();
                table.AddColumn("ID");
                table.AddColumn("Name");
                table.AddColumn("Provider");
                table.AddColumn("Version");

                foreach (var agent in agents)
                {
                    table.AddRow(
                        agent.Id,
                        agent.Name,
                        agent.Provider ?? "[grey]default[/]",
                        agent.Version);
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
