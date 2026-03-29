using System.CommandLine;
using System.Text;
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

        command.SetHandler(async agentId =>
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
                var currentThreadSpace = await kernel.ThreadSpaces.GetAsync(session.Record.ThreadSpaceId);
                var currentThreadSpaceName = currentThreadSpace.Name;

                AnsiConsole.MarkupLine("[grey]Type '/exit' or '/quit' to leave the session.[/]");
                AnsiConsole.MarkupLine("[grey]Available commands: /init, /init-proj, /clear, /remove[/]");

                while (true)
                {
                    var input = AnsiConsole.Ask<string>($"{ThemeConfig.GetUserPrefix(currentThreadSpaceName)} ");
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
                                if (await AnsiConsole.ConfirmAsync("Are you sure you want to delete all messages in this session?"))
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
                                        AnsiConsole.MarkupLine("[grey]Analyzing folder structure for intelligent agent generation...[/]");
                                        var tree = "";
                                        if (Directory.Exists(fullPath))
                                        {
                                            var entries = Directory.EnumerateFileSystemEntries(fullPath, "*", SearchOption.AllDirectories)
                                                .Take(50) // Limit for prompt safety
                                                .Select(x => Path.GetRelativePath(fullPath, x))
                                                .ToList();
                                            tree = string.Join("\n", entries);
                                        }

                                        var id = Path.GetFileName(fullPath).ToLowerInvariant();
                                        var name = Path.GetFileName(fullPath);
                                        var initPrompt = $@"请为目录 '{fullPath}' 智能生成一个项目专家型的 `agent.md` 文件。
该目录的文件结构如下：
{tree}

要求：
1. 使用 Markdown 格式，包含 YAML frontmatter。
2. ID 设置为: {id}
3. Name 设置为: {name}
4. 权限 (permissions/capabilities) 必须包含: shell.execute, file_read, file_write, system.inspect。
5. 内容应反映该项目的性质（例如：如果是 C# 项目，则作为 C# 专家），并提供详细的项目上下文和任务描述。
6. **重要**：你的回复必须【仅包含】文件内容，不要包含任何 Markdown 代码块包裹（如 ```markdown）。";

                                        await runtime.AppendUserMessageAsync(sessionId, initPrompt);
                                        AnsiConsole.Markup($"{ThemeConfig.AgentPrefix} [italic grey]Generating agent.md...[/] ");

                                        var contentBuilder = new StringBuilder();
                                        await foreach (var @event in runtime.RunTurnStreamingAsync(sessionId))
                                        {
                                            if (@event.Delta != null)
                                            {
                                                contentBuilder.Append(@event.Delta);
                                            }
                                        }
                                        
                                        var generatedContent = contentBuilder.ToString().Trim();
                                        AnsiConsole.Write(new Markdown(generatedContent));
                                        AnsiConsole.WriteLine();
                                        // Simple stripping if AI still provides code blocks
                                        if (generatedContent.StartsWith("```"))
                                        {
                                            var lines = generatedContent.Split('\n').ToList();
                                            if (lines.Count > 2)
                                            {
                                                lines.RemoveAt(0);
                                                lines.RemoveAt(lines.Count - 1);
                                                generatedContent = string.Join("\n", lines);
                                            }
                                        }

                                        await File.WriteAllTextAsync(agentFile, generatedContent);
                                        AnsiConsole.MarkupLine($"[green]Intelligently generated agent.md in {fullPath}[/]");
                                    }

                                    var threadSpace = await kernel.ThreadSpaces.GetByBoundFolderPathAsync(fullPath);
                                    if (threadSpace == null)
                                    {
                                        threadSpace = await kernel.ThreadSpaces.CreateAsync(new CreateThreadSpaceRequest(Path.GetFileName(fullPath), fullPath));
                                        AnsiConsole.MarkupLine($"[green]Created ThreadSpace: {threadSpace.Name}[/]");
                                    }
                                    else
                                    {
                                        AnsiConsole.MarkupLine($"[green]Using existing ThreadSpace: {threadSpace.Name}[/]");
                                    }
                                    currentThreadSpaceName = threadSpace.Name;

                                    await runtime.InitializeAsync(); // Reload agents
                                    session = await runtime.StartSessionAsync(new StartSessionRequest(Path.GetFileName(fullPath).ToLowerInvariant(), threadSpace.ThreadSpaceId));
                                    sessionId = session.Record.SessionId;
                                    AnsiConsole.MarkupLine($"[bold blue]Switched to new session in:[/] [green]{fullPath}[/]");
                                }
                                continue;
                            case "/init-proj":
                                {
                                    var scaffolder = host.Services.GetRequiredService<Lib.Projects.IProjectScaffolder>();
                                    var templates = await scaffolder.ListTemplatesAsync();
                                    if (templates.Count == 0)
                                    {
                                        AnsiConsole.MarkupLine("[red]No project templates found.[/]");
                                        continue;
                                    }

                                    var template = AnsiConsole.Prompt(
                                        new SelectionPrompt<Lib.Projects.ProjectTemplateDefinition>()
                                            .Title("Select a project template:")
                                            .AddChoices(templates)
                                            .UseConverter(t => $"{t.Name} ({t.Id})"));

                                    var projName = AnsiConsole.Ask<string>("Enter project name:");
                                    var targetPath = AnsiConsole.Ask("Enter target path (relative to workspace root):", projName);

                                    var result = await scaffolder.CreateProjectAsync(new Lib.Projects.CreateProjectRequest(template.Id, projName, targetPath));
                                    if (result is { IsSuccess: true, Value: not null })
                                    {
                                        AnsiConsole.MarkupLine($"[green]Project created successfully at {result.Value.ProjectRootPath}[/]");
                                        foreach (var file in result.Value.CreatedFiles)
                                        {
                                            AnsiConsole.MarkupLine($"  [grey]- {file}[/]");
                                        }

                                        // Auto-create ThreadSpace for the new project
                                        var projectRoot = result.Value.ProjectRootPath;
                                        var projectName = result.Value.ProjectName;
                                        
                                        var threadSpace = await kernel.ThreadSpaces.GetByBoundFolderPathAsync(projectRoot);
                                        if (threadSpace == null)
                                        {
                                            threadSpace = await kernel.ThreadSpaces.CreateAsync(new CreateThreadSpaceRequest(projectName, projectRoot));
                                            AnsiConsole.MarkupLine($"[green]Created ThreadSpace for project: {threadSpace.Name}[/]");
                                        }
                                        else
                                        {
                                            AnsiConsole.MarkupLine($"[green]Using existing ThreadSpace for project: {threadSpace.Name}[/]");
                                        }
                                        currentThreadSpaceName = threadSpace.Name;

                                        // Try to find if an agent was created (templates might have one)
                                        await runtime.InitializeAsync(); // Reload agents
                                        
                                        var invariant = projectName.ToLowerInvariant();
                                        if (kernel.Agents.GetAll().All(a => a.Id != invariant))
                                        {
                                            invariant = finalAgentId; // Fallback to current agent or default
                                        }

                                        session = await runtime.StartSessionAsync(new StartSessionRequest(invariant, threadSpace.ThreadSpaceId));
                                        sessionId = session.Record.SessionId;
                                        AnsiConsole.MarkupLine($"[bold blue]Switched to project session in:[/] [green]{projectRoot}[/]");
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

                    var responseBuilder = new StringBuilder();
                    var hasTextOutput = false;
                    var markdown = new Markdown("");

                    await AnsiConsole.Live(markdown)
                        .AutoClear(false)
                        .StartAsync(async ctx =>
                        {
                            await foreach (var @event in runtime.RunTurnStreamingAsync(sessionId))
                            {
                                if (@event.Delta != null)
                                {
                                    hasTextOutput = true;
                                    responseBuilder.Append(@event.Delta);
                                    markdown.Update(responseBuilder.ToString());
                                    ctx.Refresh();
                                }
                            }
                        });
                    
                    if (!hasTextOutput)
                    {
                        AnsiConsole.MarkupLine("[grey](No text response from agent)[/]");
                    }
                    
                    AnsiConsole.WriteLine();
                }

                return 0;
            });
        }, agentIdArg);

        return command;
    }
}
