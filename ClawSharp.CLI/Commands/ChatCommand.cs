using System.CommandLine;
using ClawSharp.CLI.Infrastructure;
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;

namespace ClawSharp.CLI.Commands;

public static class ChatCommand
{
    public static Command Create(IHost host)
    {
        var command = new Command("chat", "Start a new REPL session with an agent");
        var agentIdArg = new Argument<string?>("agent-id", () => null, "The ID of the agent to chat with (optional)");
        command.AddArgument(agentIdArg);

        command.SetHandler(async (agentId) =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                var runtime = host.Services.GetRequiredService<IClawRuntime>();
                await runtime.InitializeAsync();

                var kernel = host.Services.GetRequiredService<IClawKernel>();
                var options = host.Services.GetRequiredService<ClawOptions>();

                var finalAgentId = agentId;
                if (string.IsNullOrWhiteSpace(finalAgentId))
                {
                    finalAgentId = options.Agents.DefaultAgentId;
                }

                if (string.IsNullOrWhiteSpace(finalAgentId))
                {
                    var firstAgent = kernel.Agents.GetAll().FirstOrDefault();
                    finalAgentId = firstAgent?.Id;
                }

                if (string.IsNullOrWhiteSpace(finalAgentId))
                {
                    throw new InvalidOperationException("No agents found in the registry and no default agent is configured.");
                }

                AnsiConsole.MarkupLine($"[bold blue]Starting session with agent:[/] [green]{finalAgentId.EscapeMarkup()}[/]");
                var session = await runtime.StartSessionAsync(finalAgentId);
                var sessionId = session.Record.SessionId;

                AnsiConsole.MarkupLine("[grey]Type '/exit' or '/quit' to leave the session.[/]");
                AnsiConsole.MarkupLine("[grey]Available commands: /init, /init-proj, /clear, /remove[/]");

                while (true)
                {
                    var input = AnsiConsole.Ask<string>($"{ThemeConfig.UserPrefix} ");
                    if (input == null!) break;

                    var trimmedInput = input.Trim();

                    if (string.Equals(trimmedInput, "/exit", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(trimmedInput, "/quit", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    if (trimmedInput.StartsWith("/"))
                    {
                        var parts = trimmedInput.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                        var cmd = parts[0].ToLowerInvariant();
                        var args = parts.Length > 1 ? parts[1] : string.Empty;

                        switch (cmd)
                        {
                            case "/clear":
                                AnsiConsole.Clear();
                                continue;
                            case "/remove":
                                if (AnsiConsole.Confirm("Are you sure you want to delete all messages in this session?"))
                                {
                                    await runtime.DeleteSessionDataAsync(sessionId);
                                    AnsiConsole.MarkupLine("[yellow]Session history removed.[/]");
                                }
                                continue;
                            case "/init":
                                {
                                    var path = string.IsNullOrWhiteSpace(args)
                                        ? AnsiConsole.Ask<string>("Enter directory path to initialize (relative to workspace root):")
                                        : args;

                                    var fullPath = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(options.Runtime.WorkspaceRoot, path));
                                    if (!Directory.Exists(fullPath))
                                    {
                                        Directory.CreateDirectory(fullPath);
                                    }

                                    var agentFile = Path.Combine(fullPath, "agent.md");
                                    if (!File.Exists(agentFile))
                                    {
                                        await File.WriteAllTextAsync(agentFile, $@"---
id: {Path.GetFileName(fullPath).ToLowerInvariant()}
name: {Path.GetFileName(fullPath)} Agent
description: Automatically generated agent for this folder.
---
# Hello, I am your agent for this folder.
");
                                        AnsiConsole.MarkupLine($"[green]Created agent.md in {fullPath}[/]");
                                    }

                                    var threadSpace = await kernel.ThreadSpaces.CreateAsync(new ClawSharp.Lib.Runtime.CreateThreadSpaceRequest(Path.GetFileName(fullPath), fullPath));
                                    AnsiConsole.MarkupLine($"[green]Created ThreadSpace: {threadSpace.Name}[/]");

                                    await runtime.InitializeAsync(); // Reload agents
                                    session = await runtime.StartSessionAsync(new StartSessionRequest(Path.GetFileName(fullPath).ToLowerInvariant(), threadSpace.ThreadSpaceId));
                                    sessionId = session.Record.SessionId;
                                    AnsiConsole.MarkupLine($"[bold blue]Switched to new session in:[/] [green]{fullPath}[/]");
                                }
                                continue;
                            case "/init-proj":
                                {
                                    var scaffolder = host.Services.GetRequiredService<ClawSharp.Lib.Projects.IProjectScaffolder>();
                                    var templates = await scaffolder.ListTemplatesAsync();
                                    if (templates.Count == 0)
                                    {
                                        AnsiConsole.MarkupLine("[red]No project templates found.[/]");
                                        continue;
                                    }

                                    var template = AnsiConsole.Prompt(
                                        new SelectionPrompt<ClawSharp.Lib.Projects.ProjectTemplateDefinition>()
                                            .Title("Select a project template:")
                                            .AddChoices(templates)
                                            .UseConverter(t => $"{t.Name} ({t.Id})"));

                                    var projName = AnsiConsole.Ask<string>("Enter project name:");
                                    var targetPath = AnsiConsole.Ask<string>("Enter target path (relative to workspace root):", projName);

                                    var result = await scaffolder.CreateProjectAsync(new ClawSharp.Lib.Projects.CreateProjectRequest(template.Id, projName, targetPath));
                                    if (result.IsSuccess && result.Value != null)
                                    {
                                        AnsiConsole.MarkupLine($"[green]Project created successfully at {result.Value.ProjectRootPath}[/]");
                                        foreach (var file in result.Value.CreatedFiles)
                                        {
                                            AnsiConsole.MarkupLine($"  [grey]- {file}[/]");
                                        }
                                    }
                                    else
                                    {
                                        AnsiConsole.MarkupLine($"[red]Failed to create project: {result.Error}[/]");
                                    }
                                }
                                continue;
                        }
                    }

                    await runtime.AppendUserMessageAsync(sessionId, trimmedInput);

                    AnsiConsole.Markup($"{ThemeConfig.AgentPrefix} ");

                    var hasOutput = false;
                    await foreach (var @event in runtime.RunTurnStreamingAsync(sessionId))
                    {
                        if (@event.Delta != null)
                        {
                            hasOutput = true;
                            Console.Write(@event.Delta);
                        }
                    }
                    
                    if (!hasOutput)
                    {
                        AnsiConsole.MarkupLine("[grey](No text response from agent)[/]");
                    }
                    
                    AnsiConsole.WriteLine();
                    AnsiConsole.WriteLine();
                }

                return 0;
            });
        }, agentIdArg);

        return command;
    }
}
