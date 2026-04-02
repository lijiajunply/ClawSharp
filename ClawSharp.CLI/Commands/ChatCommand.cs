using System.CommandLine;
using ClawSharp.CLI.Infrastructure;
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;

namespace ClawSharp.CLI.Commands;

public static partial class ChatCommand
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
}
