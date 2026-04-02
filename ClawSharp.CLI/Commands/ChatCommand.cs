using System.CommandLine;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClawSharp.CLI.Infrastructure;
using ClawSharp.Lib.Agents;
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
        var command = new Command("chat", I18n.T("Chat.Description"));
        var agentIdArg = new Argument<string?>("agent-id", () => null, I18n.T("Chat.AgentIdArg"));
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

            state.PromptHandler.DynamicSuggestionProvider = (cmd, arg) =>
            {
                if (cmd == "/spaces" && (arg == "show" || arg == "remove"))
                {
                    return kernel.ThreadSpaces.ListAsync(false).GetAwaiter().GetResult().Select(s => s.Name);
                }
                if (cmd == "/agents")
                {
                    return kernel.Agents.GetAll().Select(a => a.Id);
                }
                if (cmd == "/config")
                {
                    return state.Host.Services.GetRequiredService<IConfigManager>().GetAllAsync().GetAwaiter().GetResult().Keys;
                }
                return Enumerable.Empty<string>();
            };

            state.PromptHandler.CurrentDirectory = currentThreadSpace.BoundFolderPath;

            ShowWelcomeHeader(finalAgentId, currentThreadSpace.Name);

            var sessionsInSpace = await kernel.ThreadSpaces.ListSessionsAsync(currentThreadSpace.ThreadSpaceId);
            if (sessionsInSpace.Count > 1)
            {
                AnsiConsole.MarkupLine($"[grey]{I18n.T("Chat.TipResume")}[/]");
            }
            AnsiConsole.WriteLine();

            while (true)
            {
                var input = await state.PromptHandler.AskAsync(GetPrompt(state.CurrentThreadSpace, state.AgentId));
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
            throw new InvalidOperationException(I18n.T("Chat.NoAgentsFound"));
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
            "/lang" => await HandleLanguageAsync(state, arguments),
            "/clear" => HandleClear(),
            "/new" => await HandleNewSessionAsync(state),
            "/resume" => await HandleResumeAsync(state),
            "/sessions" => await HandleSessionsAsync(state, arguments),
            "/agents" => await HandleAgentsAsync(state, arguments),
            "/skills" => await HandleSkillsAsync(state),
            "/tools" => await HandleToolsAsync(state),
            "/tooltrace" => await HandleToolTraceAsync(state, arguments),
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
                    AnsiConsole.MarkupLine(I18n.T("Chat.Mcp.NoServers"));
                }
                else
                {
                    var table = new Table().Border(TableBorder.Rounded);
                    table.AddColumn(I18n.T("Chat.Mcp.Column.Name"));
                    table.AddColumn(I18n.T("Chat.Mcp.Column.Command"));
                    table.AddColumn(I18n.T("Chat.Mcp.Column.Capabilities"));
                    foreach (var s in servers) table.AddRow(s.Name.EscapeMarkup(), s.Command.EscapeMarkup(), s.Capabilities.ToString());
                    AnsiConsole.Write(table);
                }
                break;

            case "search":
                if (string.IsNullOrWhiteSpace(remainder))
                {
                    AnsiConsole.MarkupLine(I18n.T("Chat.Mcp.Usage.Search"));
                    break;
                }
                await AnsiConsole.Status().StartAsync(I18n.T("Chat.Mcp.Searching"), async ctx =>
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
                            val = AnsiConsole.Prompt(new TextPrompt<string>(prompt).PromptStyle("grey").Secret());
                        else
                            val = AnsiConsole.Ask<string>(prompt);
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
                    if (string.IsNullOrWhiteSpace(key)) continue;
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
                    AnsiConsole.MarkupLine(I18n.T("Chat.Config.KeyNotFound", key.EscapeMarkup()));
                else
                    AnsiConsole.WriteLine(configManager.IsSecret(key) ? "********" : value);
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

                for (int i = 0; i < orderedSpaces.Length; i++)
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
                    try { space = await spaceManager.GetAsync(new ThreadSpaceId(identifier)); } catch { }
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
                    grid.AddRow(I18n.T("Space.Show.Row.Archived"), space.ArchivedAt.Value.ToString("F"));
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
                            s.SessionId.Value, s.AgentId.EscapeMarkup(),
                            s.Status.ToString(), s.StartedAt.ToString("g"));
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
                    try { spaceId = (await spaceManager.GetAsync(new ThreadSpaceId(identifier))).ThreadSpaceId; } catch { }
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
        else // /cd
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

    private static async Task<CommandDispatchResult> HandlePasteAsync()
    {
        AnsiConsole.MarkupLine(I18n.T("Chat.Paste.Mode"));
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
                ?? throw new InvalidOperationException(I18n.T("Chat.Edit.StartFailed", editor));
            await process.WaitForExitAsync();

            var editedContent = await File.ReadAllTextAsync(tempFile);
            if (string.IsNullOrWhiteSpace(editedContent))
            {
                AnsiConsole.MarkupLine(I18n.T("Chat.Edit.NoContent"));
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

    private static async Task RunTurnAsync(ReplState state, string input)
    {
        var processedInput = await InputPreprocessor.ProcessAsync(input, state.PromptHandler.CurrentDirectory);
        await state.Runtime.AppendUserMessageAsync(state.SessionId, processedInput);

        var hasTextOutput = false;
        string? finalAssistantMessage = null;
        PerformanceMetrics? performance = null;
        var streamingMarkdown = new Markdown(string.Empty);
        var toolTimeline = new ToolTimeline();
        var streamingRenderable = new StreamingAssistantRenderable(streamingMarkdown, toolTimeline);

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

                    if (!string.IsNullOrWhiteSpace(@event.EventType) && @event.EventPayload is { } payload)
                    {
                        if (HandleStreamingEvent(toolTimeline, @event.EventType!, payload))
                        {
                            ctx.UpdateTarget(streamingRenderable);
                        }
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
            AnsiConsole.Write(new StreamingAssistantRenderable(new Markdown(I18n.T("Chat.NoTextResponse")), toolTimeline));
        }

        if (performance is not null)
        {
            var cacheStatus = performance.AgentLaunchPlanCacheHit ? "[green]hit[/]" : "[yellow]miss[/]";
            var mcpStatus = performance.TotalMcpConnections == 0
                ? "[grey]n/a[/]"
                : performance.McpHandshakeAvoided
                    ? $"[green]{performance.ReusedMcpConnections}/{performance.TotalMcpConnections} reused[/]"
                    : "[yellow]cold[/]";
            AnsiConsole.MarkupLine(I18n.T("Chat.TurnSummary", cacheStatus, mcpStatus));
        }

        state.LastToolTimeline = toolTimeline;

        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
    }

    private static Task<CommandDispatchResult> HandleToolTraceAsync(ReplState state, string arguments)
    {
        if (state.LastToolTimeline is null || state.LastToolTimeline.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]当前会话里还没有可展开的工具调用详情。先运行一轮包含工具调用的对话，再使用 /tooltrace。[/]");
            return Task.FromResult(CommandDispatchResult.Handled());
        }

        var snapshot = state.LastToolTimeline.CreateSnapshot();
        if (string.IsNullOrWhiteSpace(arguments))
        {
            var table = new Table().Border(TableBorder.Rounded).Expand();
            table.AddColumn(new TableColumn("[grey]#[/]").NoWrap());
            table.AddColumn(new TableColumn("[grey]Kind[/]").NoWrap());
            table.AddColumn(new TableColumn("[grey]Name[/]").NoWrap());
            table.AddColumn(new TableColumn("[grey]Status[/]").NoWrap());
            table.AddColumn(new TableColumn("[grey]Time[/]").NoWrap());
            table.AddColumn("[grey]Summary[/]");

            for (var i = 0; i < snapshot.Count; i++)
            {
                var item = snapshot[i];
                table.AddRow(
                    (i + 1).ToString(),
                    item.KindBadge,
                    item.NameMarkup,
                    item.StatusMarkup,
                    item.Elapsed.EscapeMarkup(),
                    (item.Summary ?? "[grey]No summary[/]").EscapeMarkup());
            }

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine("[grey]用 /tooltrace <编号> 查看完整参数与结果，例如 /tooltrace 1[/]");
            return Task.FromResult(CommandDispatchResult.Handled());
        }

        ToolTraceSnapshot? selected = null;
        if (arguments.Trim().Equals("last", StringComparison.OrdinalIgnoreCase))
        {
            selected = snapshot.LastOrDefault();
        }
        else if (int.TryParse(arguments.Trim(), out var index) && index >= 1 && index <= snapshot.Count)
        {
            selected = snapshot[index - 1];
        }

        if (selected is null)
        {
            AnsiConsole.MarkupLine($"[yellow]未找到工具调用详情：{arguments.EscapeMarkup()}[/]");
            return Task.FromResult(CommandDispatchResult.Handled());
        }

        var detailRows = new List<IRenderable>
        {
            new Markup($"{selected.KindBadge} {selected.NameMarkup} {selected.StatusMarkup}"),
            new Markup($"[grey]耗时:[/] {selected.Elapsed.EscapeMarkup()}")
        };

        if (!string.IsNullOrWhiteSpace(selected.Summary))
        {
            detailRows.Add(new Markup($"[grey]摘要:[/] {selected.Summary!.EscapeMarkup()}"));
        }

        detailRows.Add(new Rule("[grey]Arguments[/]"));
        detailRows.Add(new Text(selected.ArgumentsRaw ?? "{}"));
        detailRows.Add(new Rule("[grey]Result[/]"));
        detailRows.Add(new Text(selected.ResultRaw ?? "(no result)"));

        AnsiConsole.Write(new Panel(new Rows(detailRows.ToArray()))
        {
            Header = new PanelHeader($"Tool Trace #{selected.Index}"),
            Border = BoxBorder.Rounded
        });

        return Task.FromResult(CommandDispatchResult.Handled());
    }

    private static bool HandleStreamingEvent(ToolTimeline toolTimeline, string eventType, JsonElement payload)
    {
        switch (eventType)
        {
            case "worker.tool.requested":
                var requestedToolName = payload.TryGetProperty("name", out var requestedNameValue)
                    ? requestedNameValue.GetString()
                    : null;
                if (string.IsNullOrWhiteSpace(requestedToolName))
                {
                    return false;
                }

                var requestedToolId = payload.TryGetProperty("id", out var requestedIdValue)
                    ? requestedIdValue.GetString()
                    : null;
                var requestedArguments = payload.TryGetProperty("argumentsJson", out var requestedArgumentsValue)
                    ? requestedArgumentsValue.GetString()
                    : null;
                var requestedIsAgent = payload.TryGetProperty("isAgent", out var requestedIsAgentValue) &&
                                       requestedIsAgentValue.ValueKind is JsonValueKind.True;
                toolTimeline.MarkRequested(requestedToolId, requestedToolName, requestedArguments, requestedIsAgent);
                return true;

            case "worker.tool.completed":
                var completedToolId = payload.TryGetProperty("toolCallId", out var completedIdValue)
                    ? completedIdValue.GetString()
                    : null;
                if (string.IsNullOrWhiteSpace(completedToolId))
                {
                    return false;
                }

                var completedToolName = payload.TryGetProperty("toolName", out var completedNameValue)
                    ? completedNameValue.GetString()
                    : null;
                var completedStatus = payload.TryGetProperty("status", out var completedStatusValue)
                    ? completedStatusValue.GetString()
                    : null;
                JsonElement? completedPayload = payload.TryGetProperty("payload", out var completedPayloadValue)
                    ? completedPayloadValue
                    : null;
                var completedIsAgent = payload.TryGetProperty("isAgent", out var completedIsAgentValue) &&
                                       completedIsAgentValue.ValueKind is JsonValueKind.True;
                toolTimeline.MarkCompleted(completedToolId, completedToolName, completedStatus, completedPayload, completedIsAgent);
                return true;

            default:
                return false;
        }
    }

    private static void ShowWelcomeHeader(string agentId, string threadSpaceName)
    {
        var grid = new Grid().AddColumn();
        grid.AddRow(new Text(I18n.T("Chat.WelcomeHeader"), new Style(Color.Blue, decoration: Decoration.Bold)));
        grid.AddRow(new Markup($"[grey]{I18n.T("Chat.AgentLabel")}[/] [green]{agentId.EscapeMarkup()}[/]   [grey]{I18n.T("Chat.SpaceLabel")}[/] [blue]{threadSpaceName.EscapeMarkup()}[/]"));
        grid.AddRow(new Text(I18n.T("Chat.TypeHelp"), new Style(Color.Grey)));

        var panel = new Panel(grid)
        {
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 0, 1, 0)
        };
        AnsiConsole.Write(panel);
    }

    private static string GetPrompt(ThreadSpaceRecord space, string agentId)
    {
        var spaceName = space.Name;
        if (spaceName.Length > 20)
        {
            spaceName = spaceName[..17] + "...";
        }

        var displayAgentId = agentId;
        if (displayAgentId.Length > 10)
        {
            // Abbreviation: firstChar + countOfOmitted + lastChar
            displayAgentId = $"{displayAgentId[0]}{displayAgentId.Length - 2}{displayAgentId[^1]}";
        }

        var color = space.IsGlobal ? "bold blue" : "cyan";
        // To render literal [ and ], we must double them: [[ and ]]
        return $"[{color}]{spaceName.EscapeMarkup()}[/][grey][[{displayAgentId.EscapeMarkup()}]][/] > ";
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

    private static string ShortId(SessionId sessionId) =>
        sessionId.Value.Length <= 8 ? sessionId.Value : sessionId.Value[..8];

    private static string? NormalizeOutputLanguage(string? outputLanguage)
    {
        if (string.IsNullOrWhiteSpace(outputLanguage))
        {
            return null;
        }

        return outputLanguage.Trim();
    }

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
        public ToolTimeline? LastToolTimeline { get; set; }
    }

    private sealed class StreamingAssistantRenderable(Markdown markdown, ToolTimeline toolTimeline) : IRenderable
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
                new Markup(I18n.T("Chat.AgentPromptHeader"))
            };

            var toolSummary = toolTimeline.CreateRenderable();
            if (toolSummary is not null)
            {
                rows.Add(toolSummary);
            }

            if (!string.IsNullOrWhiteSpace(markdown.Content))
            {
                rows.Add(markdown.HasRichContent
                    ? markdown
                    : new Text(markdown.Content));
            }

            return new Rows(rows.ToArray());
        }
    }

    private sealed class ToolTimeline
    {
        private readonly List<ToolCallViewModel> _items = [];
        private readonly Dictionary<string, ToolCallViewModel> _byId = new(StringComparer.OrdinalIgnoreCase);
        public int Count => _items.Count;

        public void MarkRequested(string? toolCallId, string toolName, string? argumentsJson, bool isAgent)
        {
            var item = FindOrCreate(toolCallId, toolName);
            item.ToolName = toolName;
            item.IsAgent = isAgent;
            item.StartedAtUtc = DateTimeOffset.UtcNow;
            item.CompletedAtUtc = null;
            item.ArgumentsSummary = SummarizeToolRequest(toolName, argumentsJson, isAgent);
            item.ArgumentsRaw = PrettyPrintJson(argumentsJson);
            item.Status = item.IsAgent ? "Delegating" : "Running";
            item.IsCompleted = false;
            item.ResultSummary = null;
            item.ResultRaw = null;
            item.StatusStyle = "yellow";
        }

        public void MarkCompleted(string toolCallId, string? toolName, string? status, JsonElement? payload, bool isAgent)
        {
            var resolvedName = string.IsNullOrWhiteSpace(toolName) ? toolCallId : toolName;
            var item = FindOrCreate(toolCallId, resolvedName!);
            if (!string.IsNullOrWhiteSpace(toolName))
            {
                item.ToolName = toolName!;
            }
            item.IsAgent = isAgent;

            item.IsCompleted = true;
            item.CompletedAtUtc = DateTimeOffset.UtcNow;
            item.Status = status switch
            {
                "Success" => "Done",
                "Denied" => "Denied",
                "ApprovalRequired" => "Needs Approval",
                "Failed" => "Failed",
                _ => status ?? "Done"
            };
            item.StatusStyle = status switch
            {
                "Success" => "green",
                "Denied" => "red",
                "Failed" => "red",
                "ApprovalRequired" => "yellow",
                _ => "grey"
            };
            item.ResultSummary = payload is { } value
                ? SummarizeToolResult(item.ToolName, value, isAgent)
                : null;
            item.ResultRaw = payload is { } rawPayload
                ? PrettyPrintJson(rawPayload.GetRawText())
                : null;
        }

        public IRenderable? CreateRenderable()
        {
            if (_items.Count == 0)
            {
                return null;
            }

            var table = new Table()
                .Border(TableBorder.Rounded)
                .Expand();
            table.AddColumn(new TableColumn("[grey]Kind[/]").NoWrap());
            table.AddColumn(new TableColumn("[grey]Name[/]").NoWrap());
            table.AddColumn(new TableColumn("[grey]Status[/]").NoWrap());
            table.AddColumn(new TableColumn("[grey]Time[/]").NoWrap());
            table.AddColumn("[grey]Details[/]");

            foreach (var item in _items.TakeLast(6))
            {
                var detail = item.IsCompleted
                    ? item.ResultSummary ?? "[grey]No result payload[/]"
                    : item.ArgumentsSummary ?? "[grey]No arguments[/]";
                var elapsed = item.GetElapsedLabel();
                var kindBadge = item.GetKindBadge();
                var nameMarkup = item.GetNameMarkup();
                table.AddRow(
                    kindBadge,
                    nameMarkup,
                    $"[{item.StatusStyle}]{item.Status.EscapeMarkup()}[/]",
                    elapsed.EscapeMarkup(),
                    detail);
            }

            return new Rows(
                new Markup("[grey]Tool Activity[/]"),
                table);
        }

        private ToolCallViewModel FindOrCreate(string? toolCallId, string toolName)
        {
            if (!string.IsNullOrWhiteSpace(toolCallId) && _byId.TryGetValue(toolCallId, out var existing))
            {
                return existing;
            }

            var created = new ToolCallViewModel
            {
                ToolCallId = toolCallId,
                ToolName = toolName,
                IsAgent = false,
                Status = "Queued",
                StatusStyle = "grey"
            };
            _items.Add(created);
            if (!string.IsNullOrWhiteSpace(toolCallId))
            {
                _byId[toolCallId] = created;
            }

            return created;
        }

        public IReadOnlyList<ToolTraceSnapshot> CreateSnapshot()
        {
            return _items.Select((item, index) => new ToolTraceSnapshot(
                index + 1,
                item.GetKindBadge(),
                item.GetNameMarkup(),
                $"[{item.StatusStyle}]{item.Status.EscapeMarkup()}[/]",
                item.GetElapsedLabel(),
                item.IsCompleted ? item.ResultSummary ?? item.ArgumentsSummary : item.ArgumentsSummary,
                item.ArgumentsRaw,
                item.ResultRaw)).ToArray();
        }
    }

    private sealed class ToolCallViewModel
    {
        public string? ToolCallId { get; init; }
        public required string ToolName { get; set; }
        public required string Status { get; set; }
        public required string StatusStyle { get; set; }
        public bool IsAgent { get; set; }
        public bool IsCompleted { get; set; }
        public string? ArgumentsSummary { get; set; }
        public string? ResultSummary { get; set; }
        public string? ArgumentsRaw { get; set; }
        public string? ResultRaw { get; set; }
        public DateTimeOffset? StartedAtUtc { get; set; }
        public DateTimeOffset? CompletedAtUtc { get; set; }

        public string GetElapsedLabel()
        {
            if (StartedAtUtc is null)
            {
                return "-";
            }

            var end = CompletedAtUtc ?? DateTimeOffset.UtcNow;
            var elapsed = end - StartedAtUtc.Value;
            if (elapsed.TotalSeconds < 1)
            {
                return $"{Math.Max(elapsed.TotalMilliseconds, 1):0} ms";
            }

            if (elapsed.TotalMinutes < 1)
            {
                return $"{elapsed.TotalSeconds:0.0} s";
            }

            return $"{elapsed.TotalMinutes:0.0} min";
        }

        public string GetKindBadge()
        {
            var (label, color) = GetToolKind();
            return $"[{color}][[{label}]][/]";
        }

        public string GetNameMarkup()
        {
            var (_, color) = GetToolKind();
            return $"[{color}]{ToolName.EscapeMarkup()}[/]";
        }

        private (string Label, string Color) GetToolKind()
        {
            if (IsAgent)
            {
                return ("AGENT", "deeppink2");
            }

            return ToolName switch
            {
                "shell_run" => ("CMD", "orange1"),
                "file_read" or "file_list" or "file_tree" or "search_text" or "search_files" or "csv_read" or "pdf_read"
                    => ("FS-R", "deepskyblue1"),
                "file_write" => ("FS-W", "yellow1"),
                "web_search" or "web_browser" => ("NET", "springgreen3"),
                "git_ops" => ("GIT", "mediumpurple"),
                "system_info" or "system_processes" => ("SYS", "grey70"),
                "email_send" => ("MAIL", "turquoise2"),
                _ => ("TOOL", "grey70")
            };
        }
    }

    private sealed record ToolTraceSnapshot(
        int Index,
        string KindBadge,
        string NameMarkup,
        string StatusMarkup,
        string Elapsed,
        string? Summary,
        string? ArgumentsRaw,
        string? ResultRaw);

    private static string? SummarizeToolRequest(string toolName, string? argumentsJson, bool isAgent)
    {
        if (isAgent)
        {
            var delegatedQuery = TryReadJsonString(argumentsJson, "query");
            return delegatedQuery is null
                ? SummarizeJson(argumentsJson)
                : $"query: {TruncateForDisplay(delegatedQuery)}";
        }

        return toolName switch
        {
            "shell_run" => TryReadJsonString(argumentsJson, "command") is { } command
                ? $"command: {TruncateForDisplay(command)}"
                : SummarizeJson(argumentsJson),
            "file_read" or "file_write" or "file_list" or "file_tree" => TryReadJsonString(argumentsJson, "path") is { } path
                ? $"path: {TruncateForDisplay(path)}"
                : SummarizeJson(argumentsJson),
            "search_text" => BuildSearchTextSummary(argumentsJson),
            "search_files" => BuildSearchFilesSummary(argumentsJson),
            _ => SummarizeJson(argumentsJson)
        };
    }

    private static string? SummarizeToolResult(string toolName, JsonElement payload, bool isAgent)
    {
        if (isAgent)
        {
            if (payload.TryGetProperty("result", out var result))
            {
                return SummarizeJson(result);
            }

            return SummarizeJson(payload);
        }

        return toolName switch
        {
            "shell_run" => BuildShellResultSummary(payload),
            "file_read" => payload.TryGetProperty("path", out var readPath)
                ? $"read: {TruncateForDisplay(readPath.GetString() ?? string.Empty)}"
                : SummarizeJson(payload),
            "file_write" => BuildFileWriteResultSummary(payload),
            "file_list" => BuildFileListResultSummary(payload),
            "file_tree" => payload.TryGetProperty("path", out var treePath)
                ? $"tree: {TruncateForDisplay(treePath.GetString() ?? string.Empty)}"
                : SummarizeJson(payload),
            "search_text" or "search_files" => payload.ValueKind == JsonValueKind.Array
                ? $"{payload.GetArrayLength()} match(es)"
                : SummarizeJson(payload),
            _ => SummarizeJson(payload)
        };
    }

    private static string? SummarizeJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return SummarizeJson(document.RootElement);
        }
        catch (JsonException)
        {
            return TruncateForDisplay(json);
        }
    }

    private static string SummarizeJson(JsonElement element)
    {
        string summary = element.ValueKind switch
        {
            JsonValueKind.Object => SummarizeObject(element),
            JsonValueKind.Array => $"{element.GetArrayLength()} item(s)",
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            _ => element.GetRawText()
        };

        return TruncateForDisplay(summary);
    }

    private static string SummarizeObject(JsonElement element)
    {
        var parts = new List<string>();
        foreach (var property in element.EnumerateObject().Take(4))
        {
            var value = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                JsonValueKind.Array => $"{property.Value.GetArrayLength()} item(s)",
                JsonValueKind.Object => "{...}",
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => "null",
                _ => property.Value.GetRawText()
            };
            parts.Add($"{property.Name}: {value}");
        }

        if (parts.Count == 0)
        {
            return "{}";
        }

        var suffix = element.EnumerateObject().Skip(4).Any() ? ", ..." : string.Empty;
        return string.Join(", ", parts) + suffix;
    }

    private static string TruncateForDisplay(string value)
    {
        var singleLine = value
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
        return singleLine.Length > 96
            ? singleLine[..93] + "..."
            : singleLine;
    }

    private static string PrettyPrintJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "{}";
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private static string? TryReadJsonString(string? json, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? BuildSearchTextSummary(string? argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            var root = document.RootElement;
            var query = root.TryGetProperty("query", out var queryValue) ? queryValue.GetString() : null;
            var path = root.TryGetProperty("path", out var pathValue) ? pathValue.GetString() : null;
            if (query is null && path is null)
            {
                return SummarizeJson(argumentsJson);
            }

            return $"query: {TruncateForDisplay(query ?? string.Empty)}" +
                   (string.IsNullOrWhiteSpace(path) ? string.Empty : $", path: {TruncateForDisplay(path)}");
        }
        catch (JsonException)
        {
            return SummarizeJson(argumentsJson);
        }
    }

    private static string? BuildSearchFilesSummary(string? argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            var root = document.RootElement;
            var pattern = root.TryGetProperty("pattern", out var patternValue) ? patternValue.GetString() : null;
            var path = root.TryGetProperty("path", out var pathValue) ? pathValue.GetString() : null;
            if (pattern is null && path is null)
            {
                return SummarizeJson(argumentsJson);
            }

            return $"pattern: {TruncateForDisplay(pattern ?? string.Empty)}" +
                   (string.IsNullOrWhiteSpace(path) ? string.Empty : $", path: {TruncateForDisplay(path)}");
        }
        catch (JsonException)
        {
            return SummarizeJson(argumentsJson);
        }
    }

    private static string BuildShellResultSummary(JsonElement payload)
    {
        var parts = new List<string>();
        if (payload.TryGetProperty("exitCode", out var exitCode))
        {
            parts.Add($"exit: {exitCode.GetRawText()}");
        }

        if (payload.TryGetProperty("stdout", out var stdout))
        {
            var stdoutText = stdout.GetString();
            if (!string.IsNullOrWhiteSpace(stdoutText))
            {
                parts.Add($"stdout: {TruncateForDisplay(stdoutText)}");
            }
        }

        if (payload.TryGetProperty("stderr", out var stderr))
        {
            var stderrText = stderr.GetString();
            if (!string.IsNullOrWhiteSpace(stderrText))
            {
                parts.Add($"stderr: {TruncateForDisplay(stderrText)}");
            }
        }

        return parts.Count == 0 ? SummarizeJson(payload) : string.Join(", ", parts);
    }

    private static string BuildFileWriteResultSummary(JsonElement payload)
    {
        var parts = new List<string>();
        if (payload.TryGetProperty("path", out var path))
        {
            parts.Add($"wrote: {TruncateForDisplay(path.GetString() ?? string.Empty)}");
        }

        if (payload.TryGetProperty("bytes", out var bytes))
        {
            parts.Add($"bytes: {bytes.GetRawText()}");
        }

        return parts.Count == 0 ? SummarizeJson(payload) : string.Join(", ", parts);
    }

    private static string BuildFileListResultSummary(JsonElement payload)
    {
        var parts = new List<string>();
        if (payload.TryGetProperty("path", out var path))
        {
            parts.Add($"path: {TruncateForDisplay(path.GetString() ?? string.Empty)}");
        }

        if (payload.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array)
        {
            parts.Add($"entries: {entries.GetArrayLength()}");
        }

        return parts.Count == 0 ? SummarizeJson(payload) : string.Join(", ", parts);
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
                        AnsiConsole.MarkupLine(I18n.T("Chat.Input.WarningReadFile", relativePath.EscapeMarkup(), ex.Message.EscapeMarkup()));
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine(I18n.T("Chat.Input.WarningFileMissing", relativePath.EscapeMarkup()));
                }
            }

            return sb.ToString();
        }
    }
}
