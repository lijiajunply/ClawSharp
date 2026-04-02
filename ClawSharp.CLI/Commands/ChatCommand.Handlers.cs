using ClawSharp.CLI.Infrastructure;
using ClawSharp.Lib.Agents;
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Hub;
using ClawSharp.Lib.Mcp;
using ClawSharp.Lib.Projects;
using ClawSharp.Lib.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace ClawSharp.CLI.Commands;

public static partial class ChatCommand
{
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
                    AnsiConsole.MarkupLine(I18n.T("Chat.Mcp.NoServers"));
                }
                else
                {
                    var table = new Table().Border(TableBorder.Rounded);
                    table.AddColumn(I18n.T("Chat.Mcp.Column.Name"));
                    table.AddColumn(I18n.T("Chat.Mcp.Column.Command"));
                    table.AddColumn(I18n.T("Chat.Mcp.Column.Capabilities"));
                    foreach (var s in servers)
                    {
                        table.AddRow(s.Name.EscapeMarkup(), s.Command.EscapeMarkup(), s.Capabilities.ToString());
                    }

                    AnsiConsole.Write(table);
                }

                break;

            case "search":
                if (string.IsNullOrWhiteSpace(remainder))
                {
                    AnsiConsole.MarkupLine(I18n.T("Chat.Mcp.Usage.Search"));
                    break;
                }

                await AnsiConsole.Status().StartAsync(I18n.T("Chat.Mcp.Searching"), async _ =>
                {
                    var results = await smithery.SearchServersAsync(remainder);
                    if (results.Count == 0)
                    {
                        AnsiConsole.MarkupLine(I18n.T("Chat.Mcp.NoSearchResults"));
                        return;
                    }

                    var table = new Table().Border(TableBorder.Rounded).Expand();
                    table.AddColumn(I18n.T("Chat.Mcp.Column.QualifiedName"));
                    table.AddColumn(I18n.T("Common.Description"));
                    table.AddColumn(I18n.T("Chat.Mcp.Column.Author"));
                    foreach (var s in results)
                    {
                        var author = s.Author is null
                            ? $"[grey]{I18n.T("Common.NotApplicable")}[/]"
                            : s.Author.EscapeMarkup();
                        table.AddRow($"[blue]{s.QualifiedName.EscapeMarkup()}[/]", s.Description.EscapeMarkup(), author);
                    }

                    AnsiConsole.Write(table);
                });
                break;

            case "install":
                if (string.IsNullOrWhiteSpace(remainder))
                {
                    AnsiConsole.MarkupLine(I18n.T("Chat.Mcp.Usage.Install"));
                    break;
                }

                var installer = state.Host.Services.GetRequiredService<IMcpInstaller>();
                var srv = await smithery.GetServerAsync(remainder);
                if (srv.McpConfig == null)
                {
                    AnsiConsole.MarkupLine(I18n.T("Chat.Mcp.NoAutomatedConfig"));
                    break;
                }

                AnsiConsole.MarkupLine(I18n.T("Chat.Mcp.Installing", srv.QualifiedName.EscapeMarkup()));
                var envs = new Dictionary<string, string>();
                if (srv.McpConfig.Env?.Count > 0)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine(I18n.T("Chat.Mcp.ConfigRequired"));
                    foreach (var env in srv.McpConfig.Env)
                    {
                        var prompt = string.IsNullOrWhiteSpace(env.Value.Description)
                            ? I18n.T("Chat.Mcp.EnvPrompt", env.Key.EscapeMarkup())
                            : I18n.T("Chat.Mcp.EnvPromptWithDescription", env.Key.EscapeMarkup(), env.Value.Description.EscapeMarkup());
                        string val;
                        if (env.Key.Contains("KEY") || env.Key.Contains("TOKEN") || env.Key.Contains("SECRET"))
                        {
                            val = AnsiConsole.Prompt(new TextPrompt<string>(prompt).PromptStyle("grey").Secret());
                        }
                        else
                        {
                            val = AnsiConsole.Ask<string>(prompt);
                        }

                        envs[env.Key] = val;
                    }
                }

                await installer.InstallAsync(srv.Name, srv.McpConfig.Command, srv.McpConfig.Args, envs);
                AnsiConsole.MarkupLine(I18n.T("Chat.Mcp.InstallSuccess", srv.Name.EscapeMarkup()));
                AnsiConsole.MarkupLine(I18n.T("Chat.Mcp.ConfigPathMessage"));
                AnsiConsole.MarkupLine(I18n.T("Chat.Mcp.ReloadMessage"));
                break;

            case "show":
                if (string.IsNullOrWhiteSpace(remainder))
                {
                    AnsiConsole.MarkupLine(I18n.T("Chat.Mcp.Usage.Show"));
                    break;
                }

                var server = await smithery.GetServerAsync(remainder);
                var panelContent = I18n.T(
                    "Chat.Mcp.ServerSummary",
                    server.Name.EscapeMarkup(),
                    server.QualifiedName.EscapeMarkup(),
                    Environment.NewLine,
                    (server.Author ?? I18n.T("Common.NotApplicable")).EscapeMarkup(),
                    server.DownloadCount?.ToString() ?? "0",
                    server.Description.EscapeMarkup());
                AnsiConsole.Write(new Panel(new Markup(panelContent))
                {
                    Header = new PanelHeader(I18n.T("Chat.Mcp.ServerPanel")),
                    Border = BoxBorder.Rounded
                });
                break;

            default:
                AnsiConsole.MarkupLine(I18n.T("Chat.Mcp.UnknownSubcommand", action.EscapeMarkup()));
                AnsiConsole.MarkupLine(I18n.T("Chat.Mcp.Supported"));
                break;
        }

        return CommandDispatchResult.Handled();
    }

    private static CommandDispatchResult HandleHelp()
    {
        ShowHelp();
        return CommandDispatchResult.Handled();
    }

    private static async Task<CommandDispatchResult> HandleLanguageAsync(ReplState state, string arguments)
    {
        var trimmed = arguments.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            var sessionOverride = NormalizeOutputLanguage(state.Session.Record.OutputLanguageOverride);
            var globalDefault = NormalizeOutputLanguage(state.Options.Runtime.OutputLanguage);
            var effective = sessionOverride ?? globalDefault;

            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn($"[yellow]{I18n.T("Chat.Language.Column.Scope")}[/]");
            table.AddColumn($"[yellow]{I18n.T("Chat.Language.Column.Language")}[/]");
            table.AddRow(I18n.T("Chat.Language.CurrentSession"), sessionOverride?.EscapeMarkup() ?? $"[grey]{I18n.T("Common.None")}[/]");
            table.AddRow(I18n.T("Chat.Language.GlobalDefault"), globalDefault?.EscapeMarkup() ?? $"[grey]{I18n.T("Common.None")}[/]");
            table.AddRow(I18n.T("Chat.Language.Effective"), effective?.EscapeMarkup() ?? $"[grey]{I18n.T("Common.None")}[/]");
            AnsiConsole.Write(table);
            return CommandDispatchResult.Handled();
        }

        if (trimmed.Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            state.Session = await state.Runtime.UpdateSessionOutputLanguageAsync(state.SessionId, null);
            state.SessionId = state.Session.Record.SessionId;
            var fallback = NormalizeOutputLanguage(state.Options.Runtime.OutputLanguage);
            if (fallback is null)
            {
                AnsiConsole.MarkupLine(I18n.T("Chat.Language.ResetNoFallback"));
            }
            else
            {
                AnsiConsole.MarkupLine(I18n.T("Chat.Language.ResetWithFallback", fallback.EscapeMarkup()));
            }

            return CommandDispatchResult.Handled();
        }

        var outputLanguage = NormalizeOutputLanguage(trimmed);
        state.Session = await state.Runtime.UpdateSessionOutputLanguageAsync(state.SessionId, outputLanguage);
        state.SessionId = state.Session.Record.SessionId;
        AnsiConsole.MarkupLine(I18n.T("Chat.Language.Updated", outputLanguage!.EscapeMarkup()));
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
        AnsiConsole.MarkupLine(I18n.T("Chat.NewSessionStarted"));
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
            AnsiConsole.MarkupLine(I18n.T("Chat.Resume.None"));
            return CommandDispatchResult.Handled();
        }

        await ActivateSessionAsync(state, lastSession);
        AnsiConsole.MarkupLine(I18n.T("Chat.Resume.Success", lastSession.StartedAt.ToString("g")));
        await ReplayRecentHistoryAsync(state.Runtime, state.SessionId);
        return CommandDispatchResult.Handled();
    }

    private static async Task<CommandDispatchResult> HandleSessionsAsync(ReplState state, string arguments)
    {
        var sessions = await state.Kernel.ThreadSpaces.ListSessionsAsync(state.CurrentThreadSpace.ThreadSpaceId);
        if (sessions.Count == 0)
        {
            AnsiConsole.MarkupLine(I18n.T("Chat.Sessions.None"));
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
            AnsiConsole.MarkupLine(I18n.T("Chat.Sessions.IndexOutOfRange", orderedSessions.Length));
            return CommandDispatchResult.Handled();
        }

        var selectedSession = orderedSessions[selectedIndex - 1];
        await ActivateSessionAsync(state, selectedSession);
        AnsiConsole.MarkupLine(I18n.T("Chat.Sessions.Switched", selectedIndex, ShortId(selectedSession.SessionId)));
        await ReplayRecentHistoryAsync(state.Runtime, state.SessionId);
        return CommandDispatchResult.Handled();
    }

    private static async Task ShowSessionsTableAsync(ReplState state, IReadOnlyList<SessionRecord> sessions)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn($"[yellow]{I18n.T("Chat.Sessions.Column.Index")}[/]");
        table.AddColumn($"[yellow]{I18n.T("Chat.Sessions.Column.SessionId")}[/]");
        table.AddColumn($"[yellow]{I18n.T("Chat.Sessions.Column.Agent")}[/]");
        table.AddColumn($"[yellow]{I18n.T("Chat.Sessions.Column.Started")}[/]");
        table.AddColumn($"[yellow]{I18n.T("Chat.Sessions.Column.LastMessage")}[/]");

        for (var index = 0; index < sessions.Count; index++)
        {
            var session = sessions[index];
            var history = await state.Kernel.History.ListAsync(session.SessionId);
            var preview = history.LastOrDefault()?.Content ?? I18n.T("Chat.Sessions.NoMessagesYet");
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
            AnsiConsole.MarkupLine(I18n.T("Chat.Tools.None"));
            return CommandDispatchResult.Handled();
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn($"[yellow]{I18n.T("Chat.Tools.Column.Tool")}[/]");
        table.AddColumn($"[yellow]{I18n.T("Chat.Tools.Column.Description")}[/]");
        table.AddColumn($"[yellow]{I18n.T("Chat.Tools.Column.PermissionStatus")}[/]");

        foreach (var tool in launchPlan.Tools.OrderBy(tool => tool.Name, StringComparer.OrdinalIgnoreCase))
        {
            var capability = tool.Capabilities == ClawSharp.Lib.Tools.ToolCapability.None
                ? I18n.T("Chat.Tools.NoExtraCapability")
                : tool.Capabilities.ToString();
            var status = launchPlan.Session.EffectivePermissions?.ApprovalRequired == true
                ? I18n.T("Chat.Tools.AuthorizedApproval", capability)
                : I18n.T("Chat.Tools.Authorized", capability);

            table.AddRow(tool.Name.EscapeMarkup(), tool.Description.EscapeMarkup(), status.EscapeMarkup());
        }

        AnsiConsole.Write(table);
        return CommandDispatchResult.Handled();
    }

    private static async Task<CommandDispatchResult> HandleAgentsAsync(ReplState state, string arguments)
    {
        await state.Runtime.InitializeAsync();
        var allAgents = state.Kernel.Agents.GetAll().OrderBy(a => a.Id).ToArray();

        if (string.IsNullOrWhiteSpace(arguments))
        {
            await RegistryCommands.RenderAgentsAsync(state.Host.Services);
            AnsiConsole.MarkupLine(I18n.T("Chat.Agents.Usage"));
            return CommandDispatchResult.Handled();
        }

        AgentDefinition? selectedAgent = null;
        if (int.TryParse(arguments, out var index) && index > 0 && index <= allAgents.Length)
        {
            selectedAgent = allAgents[index - 1];
        }
        else
        {
            selectedAgent = allAgents.FirstOrDefault(a => string.Equals(a.Id, arguments, StringComparison.OrdinalIgnoreCase));
        }

        if (selectedAgent == null)
        {
            AnsiConsole.MarkupLine(I18n.T("Chat.Agents.NotFound", arguments.EscapeMarkup()));
            return CommandDispatchResult.Handled();
        }

        state.AgentId = selectedAgent.Id;
        state.Session = await state.Runtime.StartSessionAsync(new StartSessionRequest(state.AgentId, state.CurrentThreadSpace.ThreadSpaceId));
        state.SessionId = state.Session.Record.SessionId;

        AnsiConsole.MarkupLine(I18n.T("Chat.Agents.Switched", state.AgentId.EscapeMarkup()));
        return CommandDispatchResult.Handled();
    }

    private static Task<CommandDispatchResult> HandleSkillsAsync(ReplState state)
    {
        var skills = state.Kernel.Skills.GetAll()
            .OrderBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (skills.Length == 0)
        {
            AnsiConsole.MarkupLine(I18n.T("Chat.Skills.None"));
            return Task.FromResult(CommandDispatchResult.Handled());
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn($"[yellow]{I18n.T("Chat.Skills.Column.ID")}[/]");
        table.AddColumn($"[yellow]{I18n.T("Chat.Skills.Column.Name")}[/]");
        table.AddColumn($"[yellow]{I18n.T("Chat.Skills.Column.Source")}[/]");
        table.AddColumn($"[yellow]{I18n.T("Chat.Skills.Column.Version")}[/]");

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
                table.AddColumn($"[yellow]{I18n.T("Chat.Config.Column.Key")}[/]");
                table.AddColumn($"[yellow]{I18n.T("Chat.Config.Column.Value")}[/]");

                foreach (var key in allConfigs.Keys.OrderBy(k => k))
                {
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        continue;
                    }

                    var displayValue = configManager.IsSecret(key) ? "********" : allConfigs[key];
                    table.AddRow(key.EscapeMarkup(), displayValue?.EscapeMarkup() ?? $"[grey]{I18n.T("Common.Null")}[/]");
                }

                AnsiConsole.Write(table);
                break;
            }
            case "get" when argParts.Length >= 2:
            {
                var key = argParts[1];
                var value = configManager.Get(key);
                if (value == null)
                {
                    AnsiConsole.MarkupLine(I18n.T("Chat.Config.KeyNotFound", key.EscapeMarkup()));
                }
                else
                {
                    AnsiConsole.WriteLine(configManager.IsSecret(key) ? "********" : value);
                }

                break;
            }
            case "set" when argParts.Length >= 3:
            {
                var key = argParts[1];
                var value = argParts[2];
                await configManager.SetAsync(key, value);
                AnsiConsole.MarkupLine(I18n.T("Chat.Config.SetSuccess", key.EscapeMarkup()));
                break;
            }
            default:
                AnsiConsole.MarkupLine(I18n.T("Chat.Config.Usage"));
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
            if (entry.Message == null)
            {
                continue;
            }

            var message = entry.Message;
            var panel = new Panel(new Markdown(message.Content))
            {
                Header = new PanelHeader(message.Role.ToString())
            };
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

        var spaces = await spaceManager.ListAsync(false);
        var orderedSpaces = spaces.OrderByDescending(x => x.CreatedAt).ToArray();

        switch (sub)
        {
            case "list" or "":
            {
                var table = new Table().Border(TableBorder.Rounded);
                table.AddColumn($"[yellow]{I18n.T("Chat.Spaces.Column.Index")}[/]");
                table.AddColumn($"[yellow]{I18n.T("Chat.Spaces.Column.Name")}[/]");
                table.AddColumn($"[yellow]{I18n.T("Chat.Spaces.Column.Path")}[/]");
                table.AddColumn($"[yellow]{I18n.T("Chat.Spaces.Column.Created")}[/]");
                table.AddColumn($"[yellow]{I18n.T("Chat.Spaces.Column.Status")}[/]");

                for (var i = 0; i < orderedSpaces.Length; i++)
                {
                    var sp = orderedSpaces[i];
                    var status = sp.ArchivedAt.HasValue
                        ? $"[grey]{I18n.T("Common.Archived")}[/]"
                        : $"[green]{I18n.T("Common.Active")}[/]";
                    table.AddRow(
                        (i + 1).ToString(),
                        sp.Name.EscapeMarkup(),
                        (sp.BoundFolderPath ?? I18n.T("Chat.Spaces.GlobalPath")).EscapeMarkup(),
                        sp.CreatedAt.ToString("g"),
                        status);
                }

                AnsiConsole.Write(table);
                AnsiConsole.MarkupLine(I18n.T("Chat.Spaces.Usage.List"));
                break;
            }
            case "add" when argParts.Length >= 3:
            {
                var name = argParts[1];
                var path = Path.GetFullPath(argParts[2]);
                var created = await spaceManager.CreateAsync(new CreateThreadSpaceRequest(name, path));
                AnsiConsole.MarkupLine(I18n.T("Chat.Spaces.Created", created.Name.EscapeMarkup(), path.EscapeMarkup()));
                break;
            }
            case "show" when argParts.Length >= 2:
            {
                var identifier = argParts[1];
                ThreadSpaceRecord? space = null;

                if (int.TryParse(identifier, out var index) && index > 0 && index <= orderedSpaces.Length)
                {
                    space = orderedSpaces[index - 1];
                }
                else if (Guid.TryParse(identifier, out _))
                {
                    try
                    {
                        space = await spaceManager.GetAsync(new ThreadSpaceId(identifier));
                    }
                    catch
                    {
                    }
                }

                space ??= await spaceManager.GetByNameAsync(identifier);

                if (space == null)
                {
                    AnsiConsole.MarkupLine(I18n.T("Chat.Spaces.NotFound", identifier.EscapeMarkup()));
                    break;
                }

                var grid = new Grid().AddColumn().AddColumn();
                grid.AddRow("[yellow]ID:[/]", space.ThreadSpaceId.Value);
                grid.AddRow(I18n.T("Space.Show.Row.Name"), space.Name.EscapeMarkup());
                grid.AddRow(I18n.T("Space.Show.Row.Path"), (space.BoundFolderPath ?? I18n.T("Chat.Spaces.GlobalPath")).EscapeMarkup());
                grid.AddRow(I18n.T("Space.Show.Row.Created"), space.CreatedAt.ToString("F"));
                grid.AddRow(I18n.T("Space.Show.Row.IsGlobal"), space.IsGlobal.ToString());
                if (space.ArchivedAt.HasValue)
                {
                    grid.AddRow(I18n.T("Space.Show.Row.Archived"), space.ArchivedAt.Value.ToString("F"));
                }

                AnsiConsole.Write(new Panel(grid) { Header = new PanelHeader(I18n.T("Chat.Spaces.Panel.Header")) });

                var sessions = await spaceManager.ListSessionsAsync(space.ThreadSpaceId);
                if (sessions.Count > 0)
                {
                    var sessionTable = new Table().Title(I18n.T("Chat.Spaces.SessionTable.Title")).Border(TableBorder.Rounded);
                    sessionTable.AddColumn(I18n.T("Chat.Sessions.Column.SessionId"));
                    sessionTable.AddColumn(I18n.T("Chat.Sessions.Column.Agent"));
                    sessionTable.AddColumn(I18n.T("Common.Status"));
                    sessionTable.AddColumn(I18n.T("List.Column.StartedAt"));
                    foreach (var s in sessions)
                    {
                        sessionTable.AddRow(
                            s.SessionId.Value,
                            s.AgentId.EscapeMarkup(),
                            s.Status.ToString(),
                            s.StartedAt.ToString("g"));
                    }

                    AnsiConsole.Write(sessionTable);
                }
                else
                {
                    AnsiConsole.MarkupLine(I18n.T("Chat.Spaces.NoSessions"));
                }

                break;
            }
            case "remove" when argParts.Length >= 2:
            {
                var identifier = argParts[1];
                ThreadSpaceId? spaceId = null;

                if (int.TryParse(identifier, out var index) && index > 0 && index <= orderedSpaces.Length)
                {
                    spaceId = orderedSpaces[index - 1].ThreadSpaceId;
                }
                else if (Guid.TryParse(identifier, out _))
                {
                    try
                    {
                        spaceId = (await spaceManager.GetAsync(new ThreadSpaceId(identifier))).ThreadSpaceId;
                    }
                    catch
                    {
                    }
                }

                spaceId ??= (await spaceManager.GetByNameAsync(identifier))?.ThreadSpaceId;

                if (spaceId == null)
                {
                    AnsiConsole.MarkupLine(I18n.T("Chat.Spaces.NotFound", identifier.EscapeMarkup()));
                    break;
                }

                await spaceManager.ArchiveAsync(spaceId.Value);
                AnsiConsole.MarkupLine(I18n.T("Chat.Spaces.Archived", identifier.EscapeMarkup()));
                break;
            }
            default:
                AnsiConsole.MarkupLine(I18n.T("Chat.Spaces.Usage"));
                break;
        }

        return CommandDispatchResult.Handled();
    }

    private static async Task<CommandDispatchResult> HandleHubAsync(ReplState state, string arguments)
    {
        var options = state.Host.Services.GetRequiredService<ClawOptions>();
        if (!options.Hub.Enabled || string.IsNullOrWhiteSpace(options.Hub.BaseUrl))
        {
            AnsiConsole.MarkupLine(I18n.T("Chat.Hub.NotEnabled"));
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
                    AnsiConsole.MarkupLine(I18n.T("Chat.Hub.NoSkills"));
                    break;
                }

                var table = new Table().Border(TableBorder.Rounded).Expand();
                table.AddColumn($"[yellow]{I18n.T("Chat.Hub.Column.ID")}[/]");
                table.AddColumn($"[yellow]{I18n.T("Chat.Hub.Column.Name")}[/]");
                table.AddColumn($"[yellow]{I18n.T("Chat.Hub.Column.Version")}[/]");
                table.AddColumn($"[yellow]{I18n.T("Chat.Hub.Column.Description")}[/]");

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
                var content = I18n.T(
                    "Chat.Hub.Summary",
                    skill.Name.EscapeMarkup(),
                    skill.Id.EscapeMarkup(),
                    Environment.NewLine,
                    skill.LatestVersion.EscapeMarkup(),
                    skill.Downloads.ToString(),
                    skill.Description.EscapeMarkup());
                AnsiConsole.Write(new Panel(new Markup(content))
                {
                    Header = new PanelHeader(I18n.T("Chat.Hub.Panel.Header")),
                    Border = BoxBorder.Rounded
                });
                if (!string.IsNullOrWhiteSpace(skill.Readme))
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"[bold]{I18n.T("Common.Readme")}[/]");
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
                AnsiConsole.MarkupLine(I18n.T("Chat.Hub.Installed", installed.SkillId.EscapeMarkup(), installed.Version.EscapeMarkup()));
                AnsiConsole.MarkupLine(I18n.T("Chat.Hub.Path", installed.InstallPath.EscapeMarkup()));
                break;
            }
            default:
                AnsiConsole.MarkupLine(I18n.T("Chat.Hub.Usage"));
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
        else
        {
            if (string.IsNullOrWhiteSpace(arguments))
            {
                AnsiConsole.MarkupLine(I18n.T("Chat.ThreadSpaceSwitch.MissingPath"));
                return CommandDispatchResult.Handled();
            }

            var path = Path.GetFullPath(arguments);
            if (!Directory.Exists(path))
            {
                AnsiConsole.MarkupLine(I18n.T("Chat.ThreadSpaceSwitch.DirectoryMissing", path.EscapeMarkup()));
                return CommandDispatchResult.Handled();
            }

            state.CurrentThreadSpace = await state.Kernel.ThreadSpaces.GetByBoundFolderPathAsync(path)
                ?? await state.Kernel.ThreadSpaces.CreateAsync(new CreateThreadSpaceRequest(Path.GetFileName(path), path));
        }

        state.Session = await state.Runtime.StartSessionAsync(new StartSessionRequest(state.AgentId, state.CurrentThreadSpace.ThreadSpaceId));
        state.SessionId = state.Session.Record.SessionId;
        state.PromptHandler.CurrentDirectory = state.CurrentThreadSpace.BoundFolderPath;

        AnsiConsole.MarkupLine(I18n.T("Chat.ThreadSpaceSwitch.Success", state.CurrentThreadSpace.Name.EscapeMarkup()));
        return CommandDispatchResult.Handled();
    }

    private static async Task<CommandDispatchResult> HandleInitAsync(ReplState state)
    {
        var targetDir = state.CurrentThreadSpace.BoundFolderPath ?? Directory.GetCurrentDirectory();
        var agentFile = Path.Combine(targetDir, "agent.md");

        if (File.Exists(agentFile))
        {
            AnsiConsole.MarkupLine(I18n.T("Chat.Init.FileExists", agentFile.EscapeMarkup()));
            return CommandDispatchResult.Handled();
        }

        var templatePath = Path.Combine(state.Options.Runtime.WorkspaceRoot, ".specify/templates/agent-file-template.md");
        var template = File.Exists(templatePath)
            ? await File.ReadAllTextAsync(templatePath)
            : "---\nid: my-agent\nname: My Agent\n---\n\nHello, I am your new agent.";

        await File.WriteAllTextAsync(agentFile, template);
        AnsiConsole.MarkupLine(I18n.T("Chat.Init.Created", agentFile.EscapeMarkup()));
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
        AnsiConsole.MarkupLine(I18n.T("Chat.Reload.Success"));
        return CommandDispatchResult.Handled();
    }

    private static async Task<CommandDispatchResult> HandleSpecKitAsync(ReplState state, string arguments)
    {
        var workingDirectory = state.CurrentThreadSpace.BoundFolderPath ?? Directory.GetCurrentDirectory();
        await SpecKitCommands.HandleReplAsync(state.Host, workingDirectory, arguments);
        return CommandDispatchResult.Handled();
    }

    private static CommandDispatchResult HandleUnknownCommand(string command)
    {
        AnsiConsole.MarkupLine(I18n.T("Chat.UnknownCommand", command.EscapeMarkup()));
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
            AnsiConsole.MarkupLine(I18n.T("Chat.History.None"));
            return;
        }

        AnsiConsole.MarkupLine(I18n.T("Chat.History.Recent"));
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

    private static void ShowHelp()
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn($"[yellow]{I18n.T("Chat.Help.Title")}[/]");
        table.AddColumn($"[yellow]{I18n.T("Chat.Help.Description")}[/]");
        table.AddRow("/help".EscapeMarkup(), I18n.T("Chat.Help.ShowHelp").EscapeMarkup());
        table.AddRow("/lang [bcp47|reset]".EscapeMarkup(), I18n.T("Chat.Help.Language").EscapeMarkup());
        table.AddRow("/new".EscapeMarkup(), I18n.T("Chat.Help.New").EscapeMarkup());
        table.AddRow("/resume".EscapeMarkup(), I18n.T("Chat.Help.Resume").EscapeMarkup());
        table.AddRow("/sessions".EscapeMarkup(), I18n.T("Chat.Help.Sessions").EscapeMarkup());
        table.AddRow("/sessions <index>".EscapeMarkup(), I18n.T("Chat.Help.SessionSwitch").EscapeMarkup());
        table.AddRow("/agents".EscapeMarkup(), I18n.T("Chat.Help.Agents").EscapeMarkup());
        table.AddRow("/skills".EscapeMarkup(), I18n.T("Chat.Help.Skills").EscapeMarkup());
        table.AddRow("/tools".EscapeMarkup(), I18n.T("Chat.Help.Tools").EscapeMarkup());
        table.AddRow("/tooltrace [index|last]".EscapeMarkup(), "Show full arguments and results for recent tool calls".EscapeMarkup());
        table.AddRow("/config [list|get|set]".EscapeMarkup(), I18n.T("Chat.Help.Config").EscapeMarkup());
        table.AddRow("/history [session-id]".EscapeMarkup(), I18n.T("Chat.Help.History").EscapeMarkup());
        table.AddRow("/stats [period]".EscapeMarkup(), I18n.T("Chat.Help.Stats").EscapeMarkup());
        table.AddRow("/spaces [list|add|show|remove]".EscapeMarkup(), I18n.T("Chat.Help.Spaces").EscapeMarkup());
        table.AddRow("/hub [search|show|install]".EscapeMarkup(), I18n.T("Chat.Help.Hub").EscapeMarkup());
        table.AddRow("/paste".EscapeMarkup(), I18n.T("Chat.Help.Paste").EscapeMarkup());
        table.AddRow("/edit".EscapeMarkup(), I18n.T("Chat.Help.Edit").EscapeMarkup());
        table.AddRow("/cd <path>".EscapeMarkup(), I18n.T("Chat.Help.Cd").EscapeMarkup());
        table.AddRow("/home".EscapeMarkup(), I18n.T("Chat.Help.Home").EscapeMarkup());
        table.AddRow("/clear".EscapeMarkup(), I18n.T("Chat.Help.Clear").EscapeMarkup());
        table.AddRow("/init".EscapeMarkup(), I18n.T("Chat.Help.Init").EscapeMarkup());
        table.AddRow("/init-proj".EscapeMarkup(), I18n.T("Chat.Help.InitProj").EscapeMarkup());
        table.AddRow("/reload".EscapeMarkup(), I18n.T("Chat.Help.Reload").EscapeMarkup());
        table.AddRow("/speckit".EscapeMarkup(), I18n.T("Chat.Help.SpecKit").EscapeMarkup());
        table.AddRow("/quit, /exit".EscapeMarkup(), I18n.T("Chat.Help.Exit").EscapeMarkup());
        AnsiConsole.Write(table);
    }

    internal static bool SupportsSlashCommand(string command) =>
        command.ToLowerInvariant() switch
        {
            "/help" or "/clear" or "/new" or "/resume" or "/sessions" or "/agents" or "/skills" or
            "/tools" or "/config" or "/history" or "/stats" or "/spaces" or "/hub" or "/lang" or
            "/tooltrace" or "/cd" or "/home" or "/init" or "/init-proj" or "/reload" or "/speckit" or
            "/paste" or "/edit" or "/exit" or "/quit" => true,
            _ => false
        };

    private static string? NormalizeOutputLanguage(string? outputLanguage)
    {
        if (string.IsNullOrWhiteSpace(outputLanguage))
        {
            return null;
        }

        return outputLanguage.Trim();
    }
}
