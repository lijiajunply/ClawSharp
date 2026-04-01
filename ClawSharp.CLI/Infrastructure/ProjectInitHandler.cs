using System.Text.Json;
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Projects;
using ClawSharp.Lib.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;

namespace ClawSharp.CLI.Infrastructure;

/// <summary>
/// 统一处理项目初始化的交互流程，包括 AI 辅助生成。
/// </summary>
public static class ProjectInitHandler
{
    public sealed record AiProjectContent(string Summary, string Appendix);

    public static async Task RunAsync(
        IHost host,
        string? templateId = null,
        string? projectName = null,
        string? targetPath = null,
        SessionId? existingSessionId = null)
    {
        var runtime = host.Services.GetRequiredService<IClawRuntime>();
        var scaffolder = host.Services.GetRequiredService<IProjectScaffolder>();
        var options = host.Services.GetRequiredService<ClawOptions>();

        await runtime.InitializeAsync();

        var templates = await scaffolder.ListTemplatesAsync();
        if (templates.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No project templates found.[/]");
            return;
        }

        // 1. 选择模板
        ProjectTemplateDefinition? selectedTemplate = null;
        if (!string.IsNullOrEmpty(templateId))
        {
            selectedTemplate = templates.FirstOrDefault(t => t.Id == templateId);
        }

        selectedTemplate ??= AnsiConsole.Prompt(
            new SelectionPrompt<ProjectTemplateDefinition>()
                .Title("Select a [green]project template[/]:")
                .PageSize(10)
                .AddChoices(templates)
                .UseConverter(t => $"{t.Name} ({t.Id}) - {t.Description}"));

        // 2. 项目名称
        if (string.IsNullOrWhiteSpace(projectName))
        {
            projectName = AnsiConsole.Ask<string>("Enter project name:", "my-new-project");
        }

        // 3. 目标路径
        targetPath ??= Directory.GetCurrentDirectory();
        var projectPath = Path.IsPathRooted(projectName) ? projectName : Path.Combine(targetPath, projectName);

        // 4. AI 辅助
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProjectName"] = projectName,
            ["Author"] = Environment.UserName,
            ["Date"] = DateTime.Now.ToString("yyyy-MM-dd")
        };

        if (AnsiConsole.Confirm("Use [blue]AI assistance[/] to generate project description and README content?", true))
        {
            var idea = MultilineInputCollector.Collect("Describe your project idea (press Enter twice to finish):");
            if (!string.IsNullOrWhiteSpace(idea))
            {
                var aiContent = await GenerateAiProjectContentAsync(runtime, options, idea, selectedTemplate, existingSessionId);
                if (aiContent != null)
                {
                    variables["project_summary"] = aiContent.Summary;
                    variables["ai_appendix"] = aiContent.Appendix;
                    AnsiConsole.MarkupLine("[green]AI content generated and will be applied to the project.[/]");
                }
            }
        }

        // 5. 执行创建
        var request = new CreateProjectRequest(selectedTemplate.Id, projectName, projectPath, variables);
        var result = await scaffolder.CreateProjectAsync(request);

        if (result.IsSuccess)
        {
            AnsiConsole.MarkupLine($"[bold green]Success![/] Project '{projectName}' created at [blue]{result.Value?.ProjectRootPath.EscapeMarkup()}[/]");
            if (existingSessionId != null)
            {
                AnsiConsole.MarkupLine("[grey]Type /cd <path> to switch to the new project space.[/]");
            }
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {result.Error?.EscapeMarkup() ?? "Unknown error"}");
        }
    }

    private static async Task<AiProjectContent?> GenerateAiProjectContentAsync(
        IClawRuntime runtime,
        ClawOptions options,
        string idea,
        ProjectTemplateDefinition template,
        SessionId? existingSessionId)
    {
        return await AnsiConsole.Status().StartAsync("AI is thinking...", async ctx =>
        {
            SessionId sessionId;
            if (existingSessionId.HasValue)
            {
                sessionId = existingSessionId.Value;
            }
            else
            {
                var agentId = options.Agents.DefaultAgentId ?? "supervisor";
                var session = await runtime.StartSessionAsync(agentId);
                sessionId = session.Record.SessionId;
            }

            var prompt = $"""
                          I am starting a new project called "Project Name" using the "{template.Name}" template.
                          Template Description: {template.Description}
                          My Idea: {idea}

                          Please generate two things in JSON format:
                          1. "summary": A concise one-sentence summary of the project.
                          2. "appendix": A detailed Markdown section covering project goals, research questions (if applicable), and a high-level roadmap.

                          Response MUST be a valid JSON object with "summary" and "appendix" keys.
                          """;

            await runtime.AppendUserMessageAsync(sessionId, prompt);
            var result = await runtime.RunTurnAsync(sessionId);

            try
            {
                var content = result.AssistantMessage;
                var startIndex = content.IndexOf('{');
                var endIndex = content.LastIndexOf('}');
                if (startIndex >= 0 && endIndex > startIndex)
                {
                    var json = content.Substring(startIndex, endIndex - startIndex + 1);
                    using var doc = JsonDocument.Parse(json);
                    return new AiProjectContent(
                        doc.RootElement.GetProperty("summary").GetString() ?? "",
                        doc.RootElement.GetProperty("appendix").GetString() ?? ""
                    );
                }
            }
            catch
            {
                return new AiProjectContent(idea, result.AssistantMessage);
            }

            return null;
        });
    }
}
