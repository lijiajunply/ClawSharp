using System.CommandLine;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using ClawSharp.CLI.Infrastructure;
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Hub;
using ClawSharp.Lib.Mcp;
using ClawSharp.Lib.Projects;
using ClawSharp.Lib.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace ClawSharp.CLI.Commands;

public static class ChatCommand
{
    public static Command Create(IHost host)
    {
        var command = new Command("chat", "Start a new REPL session with an agent");
        var agentIdArg = new Argument<string?>("agent-id", () => null, "The ID of the agent to chat with (optional)");
        command.AddArgument(agentIdArg);

        command.SetHandler(async agentId =>
        {
            await RunAsync(host, agentId);
        }, agentIdArg);

        return command;
    }

    public static async Task<int> RunAsync(IHost host, string? agentId)
    {
        return await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
        {
            var runtime = host.Services.GetRequiredService<IClawRuntime>();
            await runtime.InitializeAsync();

            var kernel = host.Services.GetRequiredService<IClawKernel>();
            var options = host.Services.GetRequiredService<ClawOptions>();
            var currentThreadSpace = await kernel.ThreadSpaces.GetGlobalAsync();
            var finalAgentId = ResolveAgentId(agentId, kernel, options);
            var session = await runtime.StartSessionAsync(new StartSessionRequest(finalAgentId, currentThreadSpace.ThreadSpaceId));

            var state = new ReplState(host, runtime, kernel, options)
            {
                AgentId = finalAgentId,
                CurrentThreadSpace = currentThreadSpace,
                Session = session,
                SessionId = session.Record.SessionId,
                PromptHandler = CreatePromptHandler(kernel, options)
            };

            state.PromptHandler.CurrentDirectory = currentThreadSpace.BoundFolderPath;

            ShowWelcomeHeader(finalAgentId, currentThreadSpace.Name);

            var sessionsInSpace = await kernel.ThreadSpaces.ListSessionsAsync(currentThreadSpace.ThreadSpaceId);
            if (sessionsInSpace.Count > 1)
            {
                AnsiConsole.MarkupLine("[grey]Tip: type /resume to continue last conversation.[/]");
            }
            AnsiConsole.WriteLine();

            while (true)
            {
                var input = await state.PromptHandler.AskAsync(GetPrompt(state.CurrentThreadSpace));
                var trimmedInput = input.Trim();
                if (string.IsNullOrWhiteSpace(trimmedInput))
                {
                    continue;
                }

                if (trimmedInput.StartsWith("/", StringComparison.Ordinal))
                {
                    var commandResult = await TryHandleCommandAsync(state, trimmedInput);
                    if (commandResult.ExitRequested)
                    {
                        break;
                    }

                    if (commandResult.SubmittedInput is null)
                    {
                        continue;
                    }

                    trimmedInput = commandResult.SubmittedInput.Trim();
                    if (string.IsNullOrWhiteSpace(trimmedInput))
                    {
                        continue;
                    }
                }

                await RunTurnAsync(state, trimmedInput);
            }

            return 0;
        });
    }

    private static ReplPrompt CreatePromptHandler(IClawKernel kernel, ClawOptions options)
    {
        var promptHandler = new ReplPrompt();
        promptHandler.AddDefaultSuggestions();
        promptHandler.AddSuggestions(kernel.Agents.GetAll().Select(a => a.Id));

        var historyDir = Path.Combine(options.Runtime.WorkspaceRoot, options.Runtime.DataPath);
        Directory.CreateDirectory(historyDir);
        promptHandler.LoadHistory(Path.Combine(historyDir, "cli_history.txt"));

        return promptHandler;
    }

    private static string ResolveAgentId(string? requestedAgentId, IClawKernel kernel, ClawOptions options)
    {
        var finalAgentId = requestedAgentId;
        if (string.IsNullOrWhiteSpace(finalAgentId))
        {
            finalAgentId = options.Agents.DefaultAgentId;
        }

        if (string.IsNullOrWhiteSpace(finalAgentId))
        {
            finalAgentId = kernel.Agents.GetAll().FirstOrDefault()?.Id;
        }

        if (string.IsNullOrWhiteSpace(finalAgentId))
        {
            throw new InvalidOperationException("No agents found in the registry and no default agent is configured.");
        }

        return finalAgentId;
    }

    private static async Task<CommandDispatchResult> TryHandleCommandAsync(ReplState state, string input)
    {
        var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToLowerInvariant();
        var arguments = parts.Length > 1 ? parts[1] : string.Empty;

        return command switch
        {
            "/exit" or "/quit" => CommandDispatchResult.Exit(),
            "/help" => HandleHelp(),
            "/clear" => HandleClear(),
            "/new" => await HandleNewSessionAsync(state),
            "/resume" => await HandleResumeAsync(state),
            "/sessions" => await HandleSessionsAsync(state, arguments),
            "/agents" => await HandleAgentsAsync(state),
            "/skills" => await HandleSkillsAsync(state),
            "/tools" => await HandleToolsAsync(state),
            "/config" => await HandleConfigAsync(state, arguments),
            "/history" => await HandleHistoryAsync(state, arguments),
            "/stats" => await HandleStatsAsync(state, arguments),
            "/spaces" => await HandleSpacesAsync(state, arguments),
            "/hub" => await HandleHubAsync(state, arguments),
            "/mcp" => await HandleMcpAsync(state, arguments),
            "/cd" or "/home" => await HandleThreadSpaceSwitchAsync(state, command, arguments),
            "/init" => await HandleInitAsync(state),
            "/init-proj" => await HandleInitProjectAsync(state),
            "/reload" => await HandleReloadAsync(state),
            "/speckit" => await HandleSpecKitAsync(state, arguments),
            "/paste" => await HandlePasteAsync(),
            "/edit" => await HandleEditAsync(),
            _ => HandleUnknownCommand(command)
        };
    }

    private static async Task<CommandDispatchResult> HandleMcpAsync(ReplState state, string arguments)
    {
        var parts = arguments.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var action = parts.Length == 0 ? "list" : parts[0].ToLowerInvariant();
        var remainder = parts.Length > 1 ? parts[1].Trim() : string.Empty;

        var smithery = state.Host.Services.GetRequiredService<ISmitheryClient>();
        var catalog = state.Host.Services.GetRequiredService<IMcpServerCatalog>();

        switch (action)
        {
            case "list":
                var servers = catalog.GetAll();
                if (servers.Count == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]No MCP servers configured.[/]");
                }
                else
                {
                    var table = new Table().Border(TableBorder.Rounded);
                    table.AddColumn("Name");
                    table.AddColumn("Command");
                    table.AddColumn("Capabilities");
                    foreach (var s in servers) table.AddRow(s.Name.EscapeMarkup(), s.Command.EscapeMarkup(), s.Capabilities.ToString());
                    AnsiConsole.Write(table);
                }
                break;

            case "search":
                if (string.IsNullOrWhiteSpace(remainder))
                {
                    AnsiConsole.MarkupLine("[yellow]Usage:[/] /mcp search <query>");
                    break;
                }
                await AnsiConsole.Status().StartAsync("Searching Smithery...", async ctx =>
                {
                    var results = await smithery.SearchServersAsync(remainder);
                    if (results.Count == 0)
                    {
                        AnsiConsole.MarkupLine("[yellow]No servers found in Smithery.[/]");
                        return;
                    }
                    var table = new Table().Border(TableBorder.Rounded).Expand();
                    table.AddColumn("Qualified Name");
                    table.AddColumn("Description");
                    table.AddColumn("Author");
                    foreach (var s in results) table.AddRow($"[blue]{s.QualifiedName.EscapeMarkup()}[/]", s.Description.EscapeMarkup(), (s.Author ?? "[grey]n/a[/]").EscapeMarkup());
                    AnsiConsole.Write(table);
                });
                break;

            case "install":
                if (string.IsNullOrWhiteSpace(remainder))
                {
                    AnsiConsole.MarkupLine("[yellow]Usage:[/] /mcp install <qualified-name>");
                    break;
                }
                var installer = state.Host.Services.GetRequiredService<IMcpInstaller>();
                var srv = await smithery.GetServerAsync(remainder);
                if (srv.McpConfig == null)
                {
                    AnsiConsole.MarkupLine("[red]Error: This server does not provide an automated MCP configuration.[/]");
                    break;
                }
                AnsiConsole.MarkupLine($"[blue]Installing {srv.QualifiedName.EscapeMarkup()}...[/]");
                var envs = new Dictionary<string, string>();
                if (srv.McpConfig.Env?.Count > 0)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[bold]Configuration Required:[/]");
                    foreach (var env in srv.McpConfig.Env)
                    {
                        var prompt = $"Enter value for [yellow]{env.Key.EscapeMarkup()}[/]";
                        if (!string.IsNullOrWhiteSpace(env.Value.Description)) prompt += $" ({env.Value.Description.EscapeMarkup()})";
                        string val;
                        if (env.Key.Contains("KEY") || env.Key.Contains("TOKEN") || env.Key.Contains("SECRET"))
                            val = AnsiConsole.Prompt(new TextPrompt<string>(prompt).PromptStyle("grey").Secret());
                        else
                            val = AnsiConsole.Ask<string>(prompt);
                        envs[env.Key] = val;
                    }
                }
                await installer.InstallAsync(srv.Name, srv.McpConfig.Command, srv.McpConfig.Args, envs);
                AnsiConsole.MarkupLine($"[green]Successfully installed {srv.Name.EscapeMarkup()}![/]");
                AnsiConsole.MarkupLine("The server has been added to [grey]~/.clawsharp/mcp.json[/].");
                AnsiConsole.MarkupLine("Type [blue]/reload[/] to apply changes.");
                break;

            case "show":
                if (string.IsNullOrWhiteSpace(remainder))
                {
                    AnsiConsole.MarkupLine("[yellow]Usage:[/] /mcp show <qualified-name>");
                    break;
                }
                var server = await smithery.GetServerAsync(remainder);
                var panelContent = $"[bold]{server.Name.EscapeMarkup()}[/] ([blue]{server.QualifiedName.EscapeMarkup()}[/]){Environment.NewLine}" +
                                 $"Author: {server.Author.EscapeMarkup() ?? "[grey]n/a[/]"}{Environment.NewLine}" +
                                 $"Downloads: {server.DownloadCount}{Environment.NewLine}{Environment.NewLine}" +
                                 $"{server.Description.EscapeMarkup()}";
                AnsiConsole.Write(new Panel(new Markup(panelContent)) { Header = new PanelHeader("Smithery MCP Server"), Border = BoxBorder.Rounded });
                break;

            default:
                AnsiConsole.MarkupLine($"[yellow]Unknown /mcp subcommand:[/] {action.EscapeMarkup()}");
                AnsiConsole.MarkupLine("Supported: list, search, show");
                break;
        }

        return CommandDispatchResult.Handled();
    }

    private static CommandDispatchResult HandleHelp()
    {
        ShowHelp();
        return CommandDispatchResult.Handled();
    }

    private static CommandDispatchResult HandleClear()
    {
        AnsiConsole.Clear();
        return CommandDispatchResult.Handled();
    }

    private static async Task<CommandDispatchResult> HandleNewSessionAsync(ReplState state)
    {
        state.Session = await state.Runtime.StartSessionAsync(new StartSessionRequest(state.AgentId, state.CurrentThreadSpace.ThreadSpaceId));
        state.SessionId = state.Session.Record.SessionId;
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[green]New session started.[/]");
        return CommandDispatchResult.Handled();
    }

    private static async Task<CommandDispatchResult> HandleResumeAsync(ReplState state)
    {
        var availableSessions = await state.Kernel.ThreadSpaces.ListSessionsAsync(state.CurrentThreadSpace.ThreadSpaceId);
        var lastSession = availableSessions
            .Where(s => s.SessionId != state.SessionId)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefault();

        if (lastSession is null)
        {
            AnsiConsole.MarkupLine("[yellow]No previous session found to resume.[/]");
            return CommandDispatchResult.Handled();
        }

        await ActivateSessionAsync(state, lastSession);
        AnsiConsole.MarkupLine($"[green]Resumed session started at {lastSession.StartedAt:g}[/]");
        await ReplayRecentHistoryAsync(state.Runtime, state.SessionId);
        return CommandDispatchResult.Handled();
    }

    private static async Task<CommandDispatchResult> HandleSessionsAsync(ReplState state, string arguments)
    {
        var sessions = await state.Kernel.ThreadSpaces.ListSessionsAsync(state.CurrentThreadSpace.ThreadSpaceId);
        if (sessions.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No sessions found in this space.[/]");
            return CommandDispatchResult.Handled();
        }

        var orderedSessions = sessions
            .OrderByDescending(x => x.StartedAt)
            .ToArray();

        if (string.IsNullOrWhiteSpace(arguments))
        {
            await ShowSessionsTableAsync(state, orderedSessions);
            return CommandDispatchResult.Handled();
        }

        if (!int.TryParse(arguments, out var selectedIndex) || selectedIndex < 1 || selectedIndex > orderedSessions.Length)
        {
            AnsiConsole.MarkupLine($"[yellow]Session index must be between 1 and {orderedSessions.Length}.[/]");
            return CommandDispatchResult.Handled();
        }

        var selectedSession = orderedSessions[selectedIndex - 1];
        await ActivateSessionAsync(state, selectedSession);
        AnsiConsole.MarkupLine($"[green]Switched to session {selectedIndex}: {ShortId(selectedSession.SessionId)}[/]");
        await ReplayRecentHistoryAsync(state.Runtime, state.SessionId);
        return CommandDispatchResult.Handled();
    }

    private static async Task ShowSessionsTableAsync(ReplState state, IReadOnlyList<SessionRecord> sessions)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("[yellow]#[/]");
        table.AddColumn("[yellow]Session ID[/]");
        table.AddColumn("[yellow]Agent[/]");
        table.AddColumn("[yellow]Started[/]");
        table.AddColumn("[yellow]Last Message[/]");

        for (var index = 0; index < sessions.Count; index++)
        {
            var session = sessions[index];
            var history = await state.Kernel.History.ListAsync(session.SessionId);
            var preview = history.LastOrDefault()?.Content ?? "(No messages yet)";
            if (preview.Length > 48)
            {
                preview = preview[..45] + "...";
            }

            table.AddRow(
                (index + 1).ToString(),
                ShortId(session.SessionId),
                session.AgentId,
                session.StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                preview.EscapeMarkup());
        }

        AnsiConsole.Write(table);
    }

    private static async Task<CommandDispatchResult> HandleToolsAsync(ReplState state)
    {
        var launchPlan = await state.Runtime.PrepareAgentAsync(new AgentLaunchRequest(state.Session.Record.AgentId, state.SessionId));
        if (launchPlan.Tools.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No authorized tools are available for this session.[/]");
            return CommandDispatchResult.Handled();
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("[yellow]Tool[/]");
        table.AddColumn("[yellow]Description[/]");
        table.AddColumn("[yellow]Permission Status[/]");

        foreach (var tool in launchPlan.Tools.OrderBy(tool => tool.Name, StringComparer.OrdinalIgnoreCase))
        {
            var capability = tool.Capabilities == ClawSharp.Lib.Tools.ToolCapability.None
                ? "No extra capability"
                : tool.Capabilities.ToString();
            var status = launchPlan.Session.EffectivePermissions?.ApprovalRequired == true
                ? $"Authorized ({capability}, approval required)"
                : $"Authorized ({capability})";

            table.AddRow(tool.Name.EscapeMarkup(), tool.Description.EscapeMarkup(), status.EscapeMarkup());
        }

        AnsiConsole.Write(table);
        return CommandDispatchResult.Handled();
    }

    private static async Task<CommandDispatchResult> HandleAgentsAsync(ReplState state)
    {
        await state.Runtime.InitializeAsync();
        await RegistryCommands.RenderAgentsAsync(state.Host.Services);
        return CommandDispatchResult.Handled();
    }

    private static Task<CommandDispatchResult> HandleSkillsAsync(ReplState state)
    {
        var skills = state.Kernel.Skills.GetAll()
            .OrderBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (skills.Length == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No registered skills were found.[/]");
            return Task.FromResult(CommandDispatchResult.Handled());
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("[yellow]ID[/]");
        table.AddColumn("[yellow]Name[/]");
        table.AddColumn("[yellow]Source[/]");
        table.AddColumn("[yellow]Version[/]");

        foreach (var skill in skills)
        {
            table.AddRow(
                skill.Id.EscapeMarkup(),
                skill.Name.EscapeMarkup(),
                skill.Source.ToString().EscapeMarkup(),
                skill.Version.EscapeMarkup());
        }

        AnsiConsole.Write(table);
        return Task.FromResult(CommandDispatchResult.Handled());
    }

    private static async Task<CommandDispatchResult> HandleConfigAsync(ReplState state, string arguments)
    {
        var configManager = state.Host.Services.GetRequiredService<IConfigManager>();
        var argParts = arguments.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        var sub = argParts.Length > 0 ? argParts[0].ToLowerInvariant() : "list";

        switch (sub)
        {
            case "list" or "":
            {
                var allConfigs = await configManager.GetAllAsync();
                var table = new Table().Border(TableBorder.Rounded);
                table.AddColumn("[yellow]Key[/]");
                table.AddColumn("[yellow]Value[/]");

                foreach (var key in allConfigs.Keys.OrderBy(k => k))
                {
                    if (string.IsNullOrWhiteSpace(key)) continue;
                    var displayValue = configManager.IsSecret(key) ? "********" : allConfigs[key];
                    table.AddRow(key.EscapeMarkup(), displayValue?.EscapeMarkup() ?? "[grey]<null>[/]");
                }

                AnsiConsole.Write(table);
                break;
            }
            case "get" when argParts.Length >= 2:
            {
                var key = argParts[1];
                var value = configManager.Get(key);
                if (value == null)
                    AnsiConsole.MarkupLine($"[yellow]Key not found: {key.EscapeMarkup()}[/]");
                else
                    AnsiConsole.WriteLine(configManager.IsSecret(key) ? "********" : value);
                break;
            }
            case "set" when argParts.Length >= 3:
            {
                var key = argParts[1];
                var value = argParts[2];
                await configManager.SetAsync(key, value);
                AnsiConsole.MarkupLine($"[green]Set {key.EscapeMarkup()}[/]");
                break;
            }
            default:
                AnsiConsole.MarkupLine("[grey]Usage: /config [list | get <key> | set <key> <value>][/]");
                break;
        }

        return CommandDispatchResult.Handled();
    }

    private static async Task<CommandDispatchResult> HandleHistoryAsync(ReplState state, string arguments)
    {
        var sessionId = string.IsNullOrWhiteSpace(arguments)
            ? state.SessionId
            : new SessionId(arguments.Trim());

        var history = await state.Runtime.GetHistoryAsync(sessionId);

        foreach (var entry in history.OrderBy(m => m.SequenceNo))
        {
            if (entry.Message == null) continue;
            var message = entry.Message;
            var panel = new Panel(new Markdown(message.Content));
            panel.Header = new PanelHeader(message.Role.ToString());

            panel.BorderColor(message.Role switch
            {
                PromptMessageRole.User => Color.Green,
                PromptMessageRole.Assistant => Color.Blue,
                _ => Color.Grey
            });

            AnsiConsole.Write(panel);
            AnsiConsole.WriteLine();
        }

        return CommandDispatchResult.Handled();
    }

    private static async Task<CommandDispatchResult> HandleStatsAsync(ReplState state, string arguments)
    {
        var period = string.IsNullOrWhiteSpace(arguments) ? "24h" : arguments.Trim();
        await StatsCommands.RunAsync(state.Host, period, false, false, "table");
        return CommandDispatchResult.Handled();
    }

    private static async Task<CommandDispatchResult> HandleSpacesAsync(ReplState state, string arguments)
    {
        var spaceManager = state.Host.Services.GetRequiredService<IThreadSpaceManager>();
        var argParts = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var sub = argParts.Length > 0 ? argParts[0].ToLowerInvariant() : "list";

        switch (sub)
        {
            case "list" or "":
            {
                var spaces = await spaceManager.ListAsync(false);
                var table = new Table().Border(TableBorder.Rounded);
                table.AddColumn("[yellow]ID[/]");
                table.AddColumn("[yellow]Name[/]");
                table.AddColumn("[yellow]Path[/]");
                table.AddColumn("[yellow]Created[/]");
                table.AddColumn("[yellow]Status[/]");

                foreach (var sp in spaces)
                {
                    var status = sp.ArchivedAt.HasValue ? "[grey]Archived[/]" : "[green]Active[/]";
                    table.AddRow(
                        sp.ThreadSpaceId.Value,
                        sp.Name.EscapeMarkup(),
                        sp.BoundFolderPath.EscapeMarkup(),
                        sp.CreatedAt.ToString("g"),
                        status);
                }

                AnsiConsole.Write(table);
                break;
            }
            case "add" when argParts.Length >= 3:
            {
                var name = argParts[1];
                var path = Path.GetFullPath(argParts[2]);
                var created = await spaceManager.CreateAsync(new CreateThreadSpaceRequest(name, path));
                AnsiConsole.MarkupLine($"[green]Created space '{created.Name.EscapeMarkup()}' at {path.EscapeMarkup()}[/]");
                break;
            }
            case "show" when argParts.Length >= 2:
            {
                var identifier = argParts[1];
                ThreadSpaceRecord? space = null;
                if (Guid.TryParse(identifier, out _))
                {
                    try { space = await spaceManager.GetAsync(new ThreadSpaceId(identifier)); } catch { }
                }
                space ??= await spaceManager.GetByNameAsync(identifier);

                if (space == null)
                {
                    AnsiConsole.MarkupLine($"[red]Space '{identifier.EscapeMarkup()}' not found.[/]");
                    break;
                }

                var grid = new Grid().AddColumn().AddColumn();
                grid.AddRow("[yellow]ID:[/]", space.ThreadSpaceId.Value);
                grid.AddRow("[yellow]Name:[/]", space.Name.EscapeMarkup());
                grid.AddRow("[yellow]Path:[/]", (space.BoundFolderPath ?? "[global]").EscapeMarkup());
                grid.AddRow("[yellow]Created:[/]", space.CreatedAt.ToString("F"));
                grid.AddRow("[yellow]Is Global:[/]", space.IsGlobal.ToString());
                if (space.ArchivedAt.HasValue)
                    grid.AddRow("[red]Archived:[/]", space.ArchivedAt.Value.ToString("F"));
                AnsiConsole.Write(new Panel(grid) { Header = new PanelHeader("ThreadSpace Details") });

                var sessions = await spaceManager.ListSessionsAsync(space.ThreadSpaceId);
                if (sessions.Count > 0)
                {
                    var sessionTable = new Table().Title("Sessions").Border(TableBorder.Rounded);
                    sessionTable.AddColumn("Session ID");
                    sessionTable.AddColumn("Agent");
                    sessionTable.AddColumn("Status");
                    sessionTable.AddColumn("Started At");
                    foreach (var s in sessions)
                    {
                        sessionTable.AddRow(
                            s.SessionId.Value, s.AgentId.EscapeMarkup(),
                            s.Status.ToString(), s.StartedAt.ToString("g"));
                    }
                    AnsiConsole.Write(sessionTable);
                }
                else
                {
                    AnsiConsole.MarkupLine("[grey]No sessions in this space.[/]");
                }
                break;
            }
            case "remove" when argParts.Length >= 2:
            {
                var identifier = argParts[1];
                ThreadSpaceId? spaceId = null;
                if (Guid.TryParse(identifier, out _))
                {
                    try { spaceId = (await spaceManager.GetAsync(new ThreadSpaceId(identifier))).ThreadSpaceId; } catch { }
                }
                spaceId ??= (await spaceManager.GetByNameAsync(identifier))?.ThreadSpaceId;

                if (spaceId == null)
                {
                    AnsiConsole.MarkupLine($"[red]Space '{identifier.EscapeMarkup()}' not found.[/]");
                    break;
                }

                await spaceManager.ArchiveAsync(spaceId.Value);
                AnsiConsole.MarkupLine($"[green]Space '{identifier.EscapeMarkup()}' archived.[/]");
                break;
            }
            default:
                AnsiConsole.MarkupLine("[grey]Usage: /spaces [list | add <name> <path> | show <id> | remove <id>][/]");
                break;
        }

        return CommandDispatchResult.Handled();
    }

    private static async Task<CommandDispatchResult> HandleHubAsync(ReplState state, string arguments)
    {
        var options = state.Host.Services.GetRequiredService<ClawOptions>();
        if (!options.Hub.Enabled || string.IsNullOrWhiteSpace(options.Hub.BaseUrl))
        {
            AnsiConsole.MarkupLine("[yellow]ClawHub is not enabled. Set Hub:Enabled=true and Hub:BaseUrl to use hub commands.[/]");
            return CommandDispatchResult.Handled();
        }

        var hubClient = state.Host.Services.GetRequiredService<IHubClient>();
        var argParts = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var sub = argParts.Length > 0 ? argParts[0].ToLowerInvariant() : "search";

        switch (sub)
        {
            case "search":
            {
                var query = argParts.Length > 1 ? string.Join(' ', argParts[1..]) : null;
                var results = await hubClient.SearchSkillsAsync(query);

                if (results.Count == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]No skills found.[/]");
                    break;
                }

                var table = new Table().Border(TableBorder.Rounded).Expand();
                table.AddColumn("[yellow]ID[/]");
                table.AddColumn("[yellow]Name[/]");
                table.AddColumn("[yellow]Version[/]");
                table.AddColumn("[yellow]Description[/]");

                foreach (var skill in results)
                {
                    table.AddRow(
                        skill.Id.EscapeMarkup(),
                        skill.Name.EscapeMarkup(),
                        skill.LatestVersion.EscapeMarkup(),
                        skill.Description.EscapeMarkup());
                }

                AnsiConsole.Write(table);
                break;
            }
            case "show" when argParts.Length >= 2:
            {
                var skillId = argParts[1];
                var skill = await hubClient.GetSkillAsync(skillId);
                var content =
                    $"[bold]{skill.Name.EscapeMarkup()}[/] ([blue]{skill.Id.EscapeMarkup()}[/]){Environment.NewLine}" +
                    $"Latest: [green]{skill.LatestVersion.EscapeMarkup()}[/]{Environment.NewLine}" +
                    $"Downloads: {skill.Downloads}{Environment.NewLine}" +
                    $"{skill.Description.EscapeMarkup()}";
                AnsiConsole.Write(new Panel(new Markup(content)) { Header = new PanelHeader("ClawHub Skill"), Border = BoxBorder.Rounded });
                if (!string.IsNullOrWhiteSpace(skill.Readme))
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[bold]README[/]");
                    AnsiConsole.WriteLine(skill.Readme);
                }
                break;
            }
            case "install" when argParts.Length >= 2:
            {
                var skillId = argParts[1];
                var version = argParts.Length >= 3 ? argParts[2] : null;
                var installer = state.Host.Services.GetRequiredService<IHubInstaller>();
                var detail = await hubClient.GetSkillAsync(skillId);
                var resolvedVersion = string.IsNullOrWhiteSpace(version) ? detail.LatestVersion : version;
                var package = await hubClient.DownloadSkillPackageAsync(skillId, resolvedVersion!);
                var installed = await installer.InstallAsync(package, InstallTarget.UserHome, false);
                AnsiConsole.MarkupLine($"[green]Installed[/] [blue]{installed.SkillId.EscapeMarkup()}[/] [grey]v{installed.Version.EscapeMarkup()}[/]");
                AnsiConsole.MarkupLine($"Path: [grey]{installed.InstallPath.EscapeMarkup()}[/]");
                break;
            }
            default:
                AnsiConsole.MarkupLine("[grey]Usage: /hub [search [query] | show <skill-id> | install <skill-id> [version]][/]");
                break;
        }

        return CommandDispatchResult.Handled();
    }

    private static async Task<CommandDispatchResult> HandleThreadSpaceSwitchAsync(ReplState state, string command, string arguments)
    {
        if (command == "/home")
        {
            state.CurrentThreadSpace = await state.Kernel.ThreadSpaces.GetGlobalAsync();
        }
        else // /cd
        {
            if (string.IsNullOrWhiteSpace(arguments))
            {
                AnsiConsole.MarkupLine("[red]Error: /cd requires a path argument. Use /home to switch to global space.[/]");
                return CommandDispatchResult.Handled();
            }

            var path = Path.GetFullPath(arguments);
            if (!Directory.Exists(path))
            {
                AnsiConsole.MarkupLine($"[red]Directory not found: {path.EscapeMarkup()}[/]");
                return CommandDispatchResult.Handled();
            }

            state.CurrentThreadSpace = await state.Kernel.ThreadSpaces.GetByBoundFolderPathAsync(path)
                ?? await state.Kernel.ThreadSpaces.CreateAsync(new CreateThreadSpaceRequest(Path.GetFileName(path), path));
        }

        state.Session = await state.Runtime.StartSessionAsync(new StartSessionRequest(state.AgentId, state.CurrentThreadSpace.ThreadSpaceId));
        state.SessionId = state.Session.Record.SessionId;
        state.PromptHandler.CurrentDirectory = state.CurrentThreadSpace.BoundFolderPath;

        AnsiConsole.MarkupLine($"[bold blue]Switched to space:[/] [green]{state.CurrentThreadSpace.Name.EscapeMarkup()}[/]");
        return CommandDispatchResult.Handled();
    }

    private static async Task<CommandDispatchResult> HandleInitAsync(ReplState state)
    {
        var targetDir = state.CurrentThreadSpace.BoundFolderPath ?? Directory.GetCurrentDirectory();
        var agentFile = Path.Combine(targetDir, "agent.md");

        if (File.Exists(agentFile))
        {
            AnsiConsole.MarkupLine($"[yellow]File already exists: {agentFile.EscapeMarkup()}[/]");
            return CommandDispatchResult.Handled();
        }

        var templatePath = Path.Combine(state.Options.Runtime.WorkspaceRoot, ".specify/templates/agent-file-template.md");
        var template = File.Exists(templatePath)
            ? await File.ReadAllTextAsync(templatePath)
            : "---\nid: my-agent\nname: My Agent\n---\n\nHello, I am your new agent.";

        await File.WriteAllTextAsync(agentFile, template);
        AnsiConsole.MarkupLine($"[green]Created agent definition:[/] [blue]{agentFile.EscapeMarkup()}[/]");
        await state.Runtime.InitializeAsync();
        return CommandDispatchResult.Handled();
    }

    private static async Task<CommandDispatchResult> HandleInitProjectAsync(ReplState state)
    {
        await ProjectInitHandler.RunAsync(
            state.Host,
            projectName: null,
            templateId: null,
            targetPath: state.CurrentThreadSpace.BoundFolderPath,
            existingSessionId: state.SessionId);

        return CommandDispatchResult.Handled();
    }

    private static async Task<CommandDispatchResult> HandleReloadAsync(ReplState state)
    {
        await state.Runtime.ReloadAsync();
        AnsiConsole.MarkupLine("[green]Runtime definitions and MCP pools reloaded.[/]");
        return CommandDispatchResult.Handled();
    }

    private static async Task<CommandDispatchResult> HandleSpecKitAsync(ReplState state, string arguments)
    {
        var workingDirectory = state.CurrentThreadSpace.BoundFolderPath ?? Directory.GetCurrentDirectory();
        await SpecKitCommands.HandleReplAsync(state.Host, workingDirectory, arguments);
        return CommandDispatchResult.Handled();
    }

    private static async Task<CommandDispatchResult> HandlePasteAsync()
    {
        AnsiConsole.MarkupLine("[grey]Paste mode: enter content, then submit with a single '.' line.[/]");
        var pastedContent = await MultilineInputCollector.CapturePasteAsync(ReadPasteLineAsync, "[bold magenta]Paste[/] > ");
        return string.IsNullOrWhiteSpace(pastedContent)
            ? CommandDispatchResult.Handled()
            : CommandDispatchResult.Submit(pastedContent);
    }

    private static async Task<CommandDispatchResult> HandleEditAsync()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"clawsharp-edit-{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(tempFile, string.Empty);

        try
        {
            var editor = Environment.GetEnvironmentVariable("EDITOR");
            if (string.IsNullOrWhiteSpace(editor))
            {
                editor = OperatingSystem.IsWindows() ? "notepad" : "vi";
            }

            using var process = Process.Start(CreateEditorStartInfo(editor, tempFile))
                ?? throw new InvalidOperationException($"Failed to start editor '{editor}'.");
            await process.WaitForExitAsync();

            var editedContent = await File.ReadAllTextAsync(tempFile);
            if (string.IsNullOrWhiteSpace(editedContent))
            {
                AnsiConsole.MarkupLine("[yellow]Editor closed without any content to send.[/]");
                return CommandDispatchResult.Handled();
            }

            return CommandDispatchResult.Submit(editedContent.Trim());
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    internal static ProcessStartInfo CreateEditorStartInfo(string editorCommand, string filePath)
    {
        if (OperatingSystem.IsWindows())
        {
            return new ProcessStartInfo("cmd.exe", $"/c {editorCommand} \"{filePath}\"")
            {
                UseShellExecute = false
            };
        }

        return new ProcessStartInfo("/bin/sh")
        {
            UseShellExecute = false,
            RedirectStandardInput = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            ArgumentList = { "-lc", $"{editorCommand} {EscapeShellArgument(filePath)}" }
        };
    }

    private static async Task<string?> ReadPasteLineAsync(string promptMarkup)
    {
        AnsiConsole.Markup(promptMarkup);
        return await Task.FromResult(Console.ReadLine());
    }

    private static string EscapeShellArgument(string value) =>
        $"'{value.Replace("'", "'\\''", StringComparison.Ordinal)}'";

    private static CommandDispatchResult HandleUnknownCommand(string command)
    {
        AnsiConsole.MarkupLine($"[yellow]Unknown command: {command}. Type /help to see available commands.[/]");
        return CommandDispatchResult.Handled();
    }

    private static async Task ActivateSessionAsync(ReplState state, SessionRecord record)
    {
        state.Session = await state.Kernel.Sessions.GetAsync(record.SessionId);
        state.SessionId = record.SessionId;
        state.AgentId = record.AgentId;
    }

    private static async Task ReplayRecentHistoryAsync(IClawRuntime runtime, SessionId sessionId)
    {
        var history = await runtime.GetHistoryAsync(sessionId);
        var recentMessages = history
            .Where(entry => entry.Message is not null)
            .Select(entry => entry.Message!)
            .TakeLast(5)
            .ToArray();

        if (recentMessages.Length == 0)
        {
            AnsiConsole.MarkupLine("[grey]No messages found in this session yet.[/]");
            return;
        }

        AnsiConsole.MarkupLine("[grey]Last few messages:[/]");
        foreach (var message in recentMessages)
        {
            var roleColor = message.Role switch
            {
                PromptMessageRole.User => "green",
                PromptMessageRole.Assistant => "blue",
                PromptMessageRole.Tool => "yellow",
                _ => "grey"
            };

            var summary = message.Content.Length > 80 ? message.Content[..77] + "..." : message.Content;
            AnsiConsole.MarkupLine($"[{roleColor}]- {message.Role}:[/] {summary.EscapeMarkup()}");
        }
    }

    private static async Task RunTurnAsync(ReplState state, string input)
    {
        var processedInput = await InputPreprocessor.ProcessAsync(input, state.PromptHandler.CurrentDirectory);
        await state.Runtime.AppendUserMessageAsync(state.SessionId, processedInput);

        var hasTextOutput = false;
        string? finalAssistantMessage = null;
        PerformanceMetrics? performance = null;
        var streamingMarkdown = new Markdown(string.Empty);
        var streamingRenderable = new StreamingAssistantRenderable(streamingMarkdown);

        try
        {
            await AnsiConsole.Live(streamingRenderable)
                .AutoClear(false)
                .Overflow(VerticalOverflow.Visible)
                .StartAsync(async ctx =>
            {
                await foreach (var @event in state.Runtime.RunTurnStreamingAsync(state.SessionId))
                {
                    if (@event.Delta is not null)
                    {
                        hasTextOutput = true;
                        finalAssistantMessage = string.Concat(finalAssistantMessage, @event.Delta);
                        streamingRenderable.Update(finalAssistantMessage);
                        ctx.UpdateTarget(streamingRenderable);
                    }

                    if (@event.FinalResult is not null)
                    {
                        finalAssistantMessage = @event.FinalResult.AssistantMessage;
                        performance = @event.FinalResult.Performance;
                        streamingRenderable.Update(finalAssistantMessage);
                        ctx.UpdateTarget(streamingRenderable);
                    }
                }
            })
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            CliErrorHandler.Handle(ex);
            hasTextOutput = true;
        }

        if (!hasTextOutput)
        {
            AnsiConsole.Write(new StreamingAssistantRenderable(new Markdown("(No text response from agent)")));
        }

        if (performance is not null)
        {
            var cacheStatus = performance.AgentLaunchPlanCacheHit ? "[green]hit[/]" : "[yellow]miss[/]";
            var mcpStatus = performance.TotalMcpConnections == 0
                ? "[grey]n/a[/]"
                : performance.McpHandshakeAvoided
                    ? $"[green]{performance.ReusedMcpConnections}/{performance.TotalMcpConnections} reused[/]"
                    : "[yellow]cold[/]";
            AnsiConsole.MarkupLine($"[grey]Turn summary:[/] plan cache {cacheStatus}, MCP {mcpStatus}");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
    }

    private static void ShowWelcomeHeader(string agentId, string threadSpaceName)
    {
        var grid = new Grid().AddColumn();
        grid.AddRow(new Text("ClawSharp v1.0.0", new Style(Color.Blue, decoration: Decoration.Bold)));
        grid.AddRow(new Markup($"[grey]Agent:[/] [green]{agentId.EscapeMarkup()}[/]   [grey]ThreadSpace:[/] [blue]{threadSpaceName.EscapeMarkup()}[/]"));
        grid.AddRow(new Text("Type /help for commands", new Style(Color.Grey)));

        var panel = new Panel(grid)
        {
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 0, 1, 0)
        };
        AnsiConsole.Write(panel);
    }

    private static string GetPrompt(ThreadSpaceRecord space)
    {
        var name = space.Name;
        if (name.Length > 20)
        {
            name = name[..17] + "...";
        }

        var color = space.IsGlobal ? "bold blue" : "cyan";
        return $"[{color}]{name.EscapeMarkup()}[/] > ";
    }

    private static void ShowHelp()
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("[yellow]Command[/]");
        table.AddColumn("[yellow]Description[/]");
        table.AddRow("/help".EscapeMarkup(), "Show this help message".EscapeMarkup());
        table.AddRow("/new".EscapeMarkup(), "Start a new session in current space".EscapeMarkup());
        table.AddRow("/resume".EscapeMarkup(), "Resume last session in current space".EscapeMarkup());
        table.AddRow("/sessions".EscapeMarkup(), "List sessions in the current space".EscapeMarkup());
        table.AddRow("/sessions <index>".EscapeMarkup(), "Switch to a session and replay recent history".EscapeMarkup());
        table.AddRow("/agents".EscapeMarkup(), "List registered agents with provider details".EscapeMarkup());
        table.AddRow("/skills".EscapeMarkup(), "List registered skills".EscapeMarkup());
        table.AddRow("/tools".EscapeMarkup(), "List currently authorized tools".EscapeMarkup());
        table.AddRow("/config [list|get|set]".EscapeMarkup(), "Manage configuration (e.g. /config get Providers:DefaultProvider)".EscapeMarkup());
        table.AddRow("/history [session-id]".EscapeMarkup(), "View message history (defaults to current session)".EscapeMarkup());
        table.AddRow("/stats [period]".EscapeMarkup(), "Show usage analytics (24h, 7d, 30d, all)".EscapeMarkup());
        table.AddRow("/spaces [list|add|show|remove]".EscapeMarkup(), "Manage ThreadSpaces (e.g. /spaces add myspace /path)".EscapeMarkup());
        table.AddRow("/hub [search|show|install]".EscapeMarkup(), "Browse and install skills from ClawHub".EscapeMarkup());
        table.AddRow("/paste".EscapeMarkup(), "Enter multiline paste mode and submit with '.'".EscapeMarkup());
        table.AddRow("/edit".EscapeMarkup(), "Compose a prompt in your external editor".EscapeMarkup());
        table.AddRow("/cd <path>".EscapeMarkup(), "Switch to a directory-bound space".EscapeMarkup());
        table.AddRow("/home".EscapeMarkup(), "Switch back to global space".EscapeMarkup());
        table.AddRow("/clear".EscapeMarkup(), "Clear terminal screen".EscapeMarkup());
        table.AddRow("/init".EscapeMarkup(), "Initialize an agent definition (agent.md) in current space".EscapeMarkup());
        table.AddRow("/init-proj".EscapeMarkup(), "Scaffold a new project from templates".EscapeMarkup());
        table.AddRow("/reload".EscapeMarkup(), "Reload agent/skill definitions and reset MCP pools".EscapeMarkup());
        table.AddRow("/speckit".EscapeMarkup(), "Run SpecKit feature workflows".EscapeMarkup());
        table.AddRow("/quit, /exit".EscapeMarkup(), "Exit the REPL".EscapeMarkup());
        AnsiConsole.Write(table);
    }

    internal static bool SupportsSlashCommand(string command) =>
        command.ToLowerInvariant() switch
        {
            "/help" or "/clear" or "/new" or "/resume" or "/sessions" or "/agents" or "/skills" or
            "/tools" or "/config" or "/history" or "/stats" or "/spaces" or "/hub" or
            "/cd" or "/home" or "/init" or "/init-proj" or "/reload" or "/speckit" or
            "/paste" or "/edit" or "/exit" or "/quit" => true,
            _ => false
        };

    private static string ShortId(SessionId sessionId) =>
        sessionId.Value.Length <= 8 ? sessionId.Value : sessionId.Value[..8];

    private sealed class ReplState(IHost host, IClawRuntime runtime, IClawKernel kernel, ClawOptions options)
    {
        public IHost Host { get; } = host;
        public IClawRuntime Runtime { get; } = runtime;
        public IClawKernel Kernel { get; } = kernel;
        public ClawOptions Options { get; } = options;
        public required string AgentId { get; set; }
        public required ThreadSpaceRecord CurrentThreadSpace { get; set; }
        public required RuntimeSession Session { get; set; }
        public required SessionId SessionId { get; set; }
        public required ReplPrompt PromptHandler { get; set; }
    }

    private sealed class StreamingAssistantRenderable(Markdown markdown) : IRenderable
    {
        public void Update(string? content)
        {
            markdown.Update(content ?? string.Empty);
        }

        public Measurement Measure(RenderOptions options, int maxWidth)
        {
            var renderable = CreateBodyRenderable();
            return renderable.Measure(options, maxWidth);
        }

        public IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
        {
            var renderable = CreateBodyRenderable();
            return renderable.Render(options, maxWidth);
        }

        private IRenderable CreateBodyRenderable()
        {
            var rows = new List<IRenderable>
            {
                new Markup("[bold yellow]Agent >[/]")
            };

            if (!string.IsNullOrWhiteSpace(markdown.Content))
            {
                rows.Add(markdown.HasRichContent
                    ? markdown
                    : new Text(markdown.Content));
            }

            return new Rows(rows.ToArray());
        }
    }

    private sealed record CommandDispatchResult(bool ExitRequested, string? SubmittedInput = null)
    {
        public static CommandDispatchResult Handled() => new(false);

        public static CommandDispatchResult Submit(string value) => new(false, value);

        public static CommandDispatchResult Exit() => new(true);
    }

    private static class InputPreprocessor
    {
        private static readonly Regex FileRefRegex = new(@"@(\S+)", RegexOptions.Compiled);

        public static async Task<string> ProcessAsync(string input, string? currentDirectory)
        {
            if (string.IsNullOrWhiteSpace(input) || !input.Contains("@"))
            {
                return input;
            }

            var baseDir = currentDirectory ?? Directory.GetCurrentDirectory();
            var matches = FileRefRegex.Matches(input);
            if (matches.Count == 0) return input;

            var sb = new StringBuilder(input);
            var offset = 0;

            foreach (Match match in matches)
            {
                var relativePath = match.Groups[1].Value;
                var fullPath = Path.GetFullPath(Path.Combine(baseDir, relativePath));

                if (File.Exists(fullPath))
                {
                    try
                    {
                        var content = await File.ReadAllTextAsync(fullPath);
                        var fileName = Path.GetFileName(fullPath);
                        var ext = Path.GetExtension(fullPath).TrimStart('.');
                        
                        var replacement = $"\n\n[File: {relativePath}]\n```{ext}\n{content}\n```\n";
                        
                        sb.Remove(match.Index + offset, match.Length);
                        sb.Insert(match.Index + offset, replacement);
                        offset += replacement.Length - match.Length;
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[yellow]Warning: Could not read file {relativePath}: {ex.Message}[/]");
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine($"[yellow]Warning: File not found: {relativePath}[/]");
                }
            }

            return sb.ToString();
        }
    }
}
