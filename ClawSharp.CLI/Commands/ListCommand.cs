using System.CommandLine;
using ClawSharp.CLI.Infrastructure;
using ClawSharp.Lib.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;

namespace ClawSharp.CLI.Commands;

public static class ListCommand
{
    public static Command Create(IHost host)
    {
        var command = new Command("list", I18n.T("List.Description"));

        command.SetHandler(async () =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                var runtime = host.Services.GetRequiredService<IClawRuntime>();
                await runtime.InitializeAsync();

                var kernel = host.Services.GetRequiredService<IClawKernel>();
                var globalSpace = await kernel.ThreadSpaces.GetGlobalAsync();
                var sessions = await kernel.ThreadSpaces.ListSessionsAsync(globalSpace.ThreadSpaceId);

                var table = new Table();
                table.AddColumn(I18n.T("List.Column.SessionId"));
                table.AddColumn(I18n.T("List.Column.AgentId"));
                table.AddColumn(I18n.T("List.Column.StartedAt"));
                table.AddColumn(I18n.T("List.Column.Status"));

                foreach (var session in sessions.OrderByDescending(s => s.StartedAt))
                {
                    table.AddRow(
                        session.SessionId.Value,
                        session.AgentId,
                        session.StartedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                        session.Status.ToString());
                }

                AnsiConsole.Write(table);
                return 0;
            });
        });

        return command;
    }
}
