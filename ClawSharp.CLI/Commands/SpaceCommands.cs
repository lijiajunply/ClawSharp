using System.CommandLine;
using ClawSharp.CLI.Infrastructure;
using ClawSharp.Lib.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;

namespace ClawSharp.CLI.Commands;

public static class SpaceCommands
{
    public static Command Create(IHost host)
    {
        var command = new Command("spaces", "Manage ThreadSpaces (working containers for sessions)");

        command.AddCommand(CreateListCommand(host));
        command.AddCommand(CreateAddCommand(host));
        command.AddCommand(CreateShowCommand(host));
        command.AddCommand(CreateUpdateCommand(host));
        command.AddCommand(CreateRemoveCommand(host));

        return command;
    }

    private static Command CreateListCommand(IHost host)
    {
        var command = new Command("list", "List all ThreadSpaces");
        var allOption = new Option<bool>("--all", "Include archived ThreadSpaces");
        command.AddOption(allOption);

        command.SetHandler(async (includeArchived) =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                var runtime = host.Services.GetRequiredService<IClawRuntime>();
                await runtime.InitializeAsync();

                var spaceManager = host.Services.GetRequiredService<IThreadSpaceManager>();
                var spaces = await spaceManager.ListAsync(includeArchived);

                var table = new Table();
                table.AddColumn("ID");
                table.AddColumn("Name");
                table.AddColumn("Path");
                table.AddColumn("Created At");
                table.AddColumn("Status");

                foreach (var space in spaces)
                {
                    var status = space.ArchivedAt.HasValue ? "[grey]Archived[/]" : "[green]Active[/]";
                    table.AddRow(
                        space.ThreadSpaceId.Value,
                        space.Name.EscapeMarkup(),
                        space.BoundFolderPath.EscapeMarkup(),
                        space.CreatedAt.ToString("g"),
                        status);
                }

                AnsiConsole.Write(table);
                return 0;
            });
        }, allOption);
        return command;
    }

    private static Command CreateAddCommand(IHost host)
    {
        var command = new Command("add", "Add a new ThreadSpace");
        var nameArgument = new Argument<string>("name", "The name of the ThreadSpace");
        var pathArgument = new Argument<string>("path", "The local folder path to bind");
        
        command.AddArgument(nameArgument);
        command.AddArgument(pathArgument);

        command.SetHandler(async (name, path) =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                var runtime = host.Services.GetRequiredService<IClawRuntime>();
                await runtime.InitializeAsync();

                var spaceManager = host.Services.GetRequiredService<IThreadSpaceManager>();
                var absolutePath = Path.GetFullPath(path);
                
                var request = new CreateThreadSpaceRequest(name, absolutePath);
                var space = await spaceManager.CreateAsync(request);

                AnsiConsole.MarkupLine($"[green]ThreadSpace '{space.Name}' created successfully.[/]");
                AnsiConsole.MarkupLine($"[grey]ID: {space.ThreadSpaceId.Value}[/]");
                AnsiConsole.MarkupLine($"[grey]Path: {space.BoundFolderPath}[/]");
                
                return 0;
            });
        }, nameArgument, pathArgument);

        return command;
    }

    private static Command CreateShowCommand(IHost host)
    {
        var command = new Command("show", "Show details of a ThreadSpace including its sessions");
        var identifierArgument = new Argument<string>("identifier", "The name or ID of the ThreadSpace");
        command.AddArgument(identifierArgument);

        command.SetHandler(async (identifier) =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                var runtime = host.Services.GetRequiredService<IClawRuntime>();
                await runtime.InitializeAsync();

                var spaceManager = host.Services.GetRequiredService<IThreadSpaceManager>();
                
                // Try by ID first, then by Name
                ThreadSpaceRecord? space = null;
                if (Guid.TryParse(identifier, out _))
                {
                    try { space = await spaceManager.GetAsync(new ThreadSpaceId(identifier)); } catch { }
                }
                
                if (space == null)
                {
                    space = await spaceManager.GetByNameAsync(identifier);
                }

                if (space == null)
                {
                    AnsiConsole.MarkupLine($"[red]ThreadSpace '{identifier}' not found.[/]");
                    return 1;
                }

                var grid = new Grid();
                grid.AddColumn();
                grid.AddColumn();
                grid.AddRow("[yellow]ID:[/]", space.ThreadSpaceId.Value);
                grid.AddRow("[yellow]Name:[/]", space.Name.EscapeMarkup());
                grid.AddRow("[yellow]Path:[/]", space.BoundFolderPath.EscapeMarkup());
                grid.AddRow("[yellow]Created:[/]", space.CreatedAt.ToString("F"));
                grid.AddRow("[yellow]Is Init:[/]", space.IsInit.ToString());
                if (space.ArchivedAt.HasValue)
                {
                    grid.AddRow("[red]Archived:[/]", space.ArchivedAt.Value.ToString("F"));
                }
                
                AnsiConsole.Write(new Panel(grid) { Header = new PanelHeader("ThreadSpace Details") });

                var sessions = await spaceManager.ListSessionsAsync(space.ThreadSpaceId);
                if (sessions.Count > 0)
                {
                    var sessionTable = new Table().Title("Sessions");
                    sessionTable.AddColumn("Session ID");
                    sessionTable.AddColumn("Agent");
                    sessionTable.AddColumn("Status");
                    sessionTable.AddColumn("Started At");

                    foreach (var session in sessions)
                    {
                        sessionTable.AddRow(
                            session.SessionId.Value,
                            session.AgentId.EscapeMarkup(),
                            session.Status.ToString(),
                            session.StartedAt.ToString("g"));
                    }
                    AnsiConsole.Write(sessionTable);
                }
                else
                {
                    AnsiConsole.MarkupLine("[grey]No sessions found in this ThreadSpace.[/]");
                }

                return 0;
            });
        }, identifierArgument);

        return command;
    }

    private static Command CreateUpdateCommand(IHost host)
    {
        var command = new Command("update", "Update a ThreadSpace's name or path");
        var identifierArgument = new Argument<string>("identifier", "The name or ID of the ThreadSpace to update");
        var nameOption = new Option<string>("--name", "The new name");
        var pathOption = new Option<string>("--path", "The new local folder path to bind");

        command.AddArgument(identifierArgument);
        command.AddOption(nameOption);
        command.AddOption(pathOption);

        command.SetHandler(async (identifier, newName, newPath) =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                var runtime = host.Services.GetRequiredService<IClawRuntime>();
                await runtime.InitializeAsync();

                var spaceManager = host.Services.GetRequiredService<IThreadSpaceManager>();
                var spaceId = await ResolveThreadSpaceId(spaceManager, identifier);
                
                if (spaceId == null)
                {
                    AnsiConsole.MarkupLine($"[red]ThreadSpace '{identifier}' not found.[/]");
                    return 1;
                }

                var updated = await spaceManager.UpdateAsync(spaceId.Value, newName, newPath);

                AnsiConsole.MarkupLine($"[green]ThreadSpace '{updated.Name}' updated successfully.[/]");
                return 0;
            });
        }, identifierArgument, nameOption, pathOption);

        return command;
    }

    private static Command CreateRemoveCommand(IHost host)
    {
        var command = new Command("remove", "Archive a ThreadSpace (soft delete)");
        var identifierArgument = new Argument<string>("identifier", "The name or ID of the ThreadSpace to archive");
        command.AddArgument(identifierArgument);

        command.SetHandler(async (identifier) =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                var runtime = host.Services.GetRequiredService<IClawRuntime>();
                await runtime.InitializeAsync();

                var spaceManager = host.Services.GetRequiredService<IThreadSpaceManager>();
                var spaceId = await ResolveThreadSpaceId(spaceManager, identifier);
                
                if (spaceId == null)
                {
                    AnsiConsole.MarkupLine($"[red]ThreadSpace '{identifier}' not found.[/]");
                    return 1;
                }

                await spaceManager.ArchiveAsync(spaceId.Value);

                AnsiConsole.MarkupLine($"[green]ThreadSpace '{identifier}' has been archived.[/]");
                return 0;
            });
        }, identifierArgument);

        return command;
    }

    private static async Task<ThreadSpaceId?> ResolveThreadSpaceId(IThreadSpaceManager spaceManager, string identifier)
    {
        if (Guid.TryParse(identifier, out _))
        {
            try
            {
                var space = await spaceManager.GetAsync(new ThreadSpaceId(identifier));
                return space.ThreadSpaceId;
            }
            catch { }
        }

        var byName = await spaceManager.GetByNameAsync(identifier);
        return byName?.ThreadSpaceId;
    }
}
