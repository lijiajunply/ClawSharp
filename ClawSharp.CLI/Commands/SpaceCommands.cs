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
        var command = new Command("spaces", I18n.T("Space.Description"));

        command.AddCommand(CreateListCommand(host));
        command.AddCommand(CreateAddCommand(host));
        command.AddCommand(CreateShowCommand(host));
        command.AddCommand(CreateUpdateCommand(host));
        command.AddCommand(CreateRemoveCommand(host));

        return command;
    }

    private static Command CreateListCommand(IHost host)
    {
        var command = new Command("list", I18n.T("Space.List.Description"));
        var allOption = new Option<bool>("--all", I18n.T("Space.List.AllOption"));
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
                table.AddColumn(I18n.T("Common.ID"));
                table.AddColumn(I18n.T("Space.List.Column.Name"));
                table.AddColumn(I18n.T("Space.List.Column.Path"));
                table.AddColumn(I18n.T("Space.List.Column.CreatedAt"));
                table.AddColumn(I18n.T("Space.List.Column.Status"));

                foreach (var space in spaces)
                {
                    var status = space.ArchivedAt.HasValue
                        ? $"[grey]{I18n.T("Common.Archived")}[/]"
                        : $"[green]{I18n.T("Common.Active")}[/]";
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
        var command = new Command("add", I18n.T("Space.Add.Description"));
        var nameArgument = new Argument<string>("name", I18n.T("Space.Add.NameArg"));
        var pathArgument = new Argument<string>("path", I18n.T("Space.Add.PathArg"));
        
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

                AnsiConsole.MarkupLine(I18n.T("Space.Add.Success", space.Name.EscapeMarkup()));
                AnsiConsole.MarkupLine(I18n.T("Space.Add.Id", space.ThreadSpaceId.Value));
                AnsiConsole.MarkupLine(I18n.T("Space.Add.Path", space.BoundFolderPath.EscapeMarkup()));
                
                return 0;
            });
        }, nameArgument, pathArgument);

        return command;
    }

    private static Command CreateShowCommand(IHost host)
    {
        var command = new Command("show", I18n.T("Space.Show.Description"));
        var identifierArgument = new Argument<string>("identifier", I18n.T("Space.Show.IdentifierArg"));
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
                    try
                    {
                        space = await spaceManager.GetAsync(new ThreadSpaceId(identifier));
                    }
                    catch
                    {
                        //
                    }
                }
                
                space ??= await spaceManager.GetByNameAsync(identifier);

                if (space == null)
                {
                    AnsiConsole.MarkupLine(I18n.T("Space.Show.NotFound", identifier.EscapeMarkup()));
                    return 1;
                }

                var grid = new Grid();
                grid.AddColumn();
                grid.AddColumn();
                grid.AddRow("[yellow]ID:[/]", space.ThreadSpaceId.Value);
                grid.AddRow(I18n.T("Space.Show.Row.Name"), space.Name.EscapeMarkup());
                grid.AddRow(I18n.T("Space.Show.Row.Path"), (space.BoundFolderPath ?? I18n.T("Space.Show.GlobalPath")).EscapeMarkup());
                grid.AddRow(I18n.T("Space.Show.Row.Created"), space.CreatedAt.ToString("F"));
                grid.AddRow(I18n.T("Space.Show.Row.IsGlobal"), space.IsGlobal.ToString());
                if (space.ArchivedAt.HasValue)
                {
                    grid.AddRow(I18n.T("Space.Show.Row.Archived"), space.ArchivedAt.Value.ToString("F"));
                }
                
                AnsiConsole.Write(new Panel(grid) { Header = new PanelHeader(I18n.T("Common.Panel.ThreadSpaceDetails")) });

                var sessions = await spaceManager.ListSessionsAsync(space.ThreadSpaceId);
                if (sessions.Count > 0)
                {
                    var sessionTable = new Table().Title(I18n.T("Common.Sessions"));
                    sessionTable.AddColumn(I18n.T("List.Column.SessionId"));
                    sessionTable.AddColumn(I18n.T("Chat.Sessions.Column.Agent"));
                    sessionTable.AddColumn(I18n.T("Common.Status"));
                    sessionTable.AddColumn(I18n.T("List.Column.StartedAt"));

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
                    AnsiConsole.MarkupLine(I18n.T("Space.Show.EmptySessions"));
                }

                return 0;
            });
        }, identifierArgument);

        return command;
    }

    private static Command CreateUpdateCommand(IHost host)
    {
        var command = new Command("update", I18n.T("Space.Update.Description"));
        var identifierArgument = new Argument<string>("identifier", I18n.T("Space.Update.IdentifierArg"));
        var nameOption = new Option<string>("--name", I18n.T("Space.Update.NameOption"));
        var pathOption = new Option<string>("--path", I18n.T("Space.Update.PathOption"));

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
                    AnsiConsole.MarkupLine(I18n.T("Space.Show.NotFound", identifier.EscapeMarkup()));
                    return 1;
                }

                var updated = await spaceManager.UpdateAsync(spaceId.Value, newName, newPath);

                AnsiConsole.MarkupLine(I18n.T("Space.Update.Success", updated.Name.EscapeMarkup()));
                return 0;
            });
        }, identifierArgument, nameOption, pathOption);

        return command;
    }

    private static Command CreateRemoveCommand(IHost host)
    {
        var command = new Command("remove", I18n.T("Space.Remove.Description"));
        var identifierArgument = new Argument<string>("identifier", I18n.T("Space.Remove.IdentifierArg"));
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
                    AnsiConsole.MarkupLine(I18n.T("Space.Show.NotFound", identifier.EscapeMarkup()));
                    return 1;
                }

                await spaceManager.ArchiveAsync(spaceId.Value);

                AnsiConsole.MarkupLine(I18n.T("Space.Remove.Success", identifier.EscapeMarkup()));
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
            catch
            {
                //
            }
        }

        var byName = await spaceManager.GetByNameAsync(identifier);
        return byName?.ThreadSpaceId;
    }
}
