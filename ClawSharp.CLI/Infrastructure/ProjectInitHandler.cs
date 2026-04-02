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
    public sealed record AiProjectContent(string Summary, string Appendix, string Plan, string Spec);

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
            AnsiConsole.MarkupLine(I18n.T("ProjectInit.NoTemplates"));
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
                .Title(I18n.T("ProjectInit.TemplateTitle"))
                .PageSize(10)
                .AddChoices(templates)
                .UseConverter(t => $"{t.Name} ({t.Id}) - {t.Description}"));

        // 2. 项目名称
        if (string.IsNullOrWhiteSpace(projectName))
        {
            projectName = AnsiConsole.Ask<string>(I18n.T("ProjectInit.ProjectNamePrompt"), "my-new-project");
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

        if (AnsiConsole.Confirm(I18n.T("ProjectInit.UseAi"), true))
        {
            var idea = MultilineInputCollector.Collect(I18n.T("ProjectInit.DescribeIdea"));
            if (!string.IsNullOrWhiteSpace(idea))
            {
                var aiContent = await GenerateAiProjectContentAsync(runtime, options, idea, selectedTemplate, existingSessionId);
                if (aiContent != null)
                {
                    variables["project_summary"] = aiContent.Summary;
                    variables["ai_appendix"] = aiContent.Appendix;
                    variables["ai_plan"] = aiContent.Plan;
                    variables["ai_spec"] = aiContent.Spec;
                    AnsiConsole.MarkupLine(I18n.T("ProjectInit.AiGenerated"));
                }
            }
        }

        // 5. 执行创建
        var request = new CreateProjectRequest(selectedTemplate.Id, projectName, projectPath, variables);
        var result = await scaffolder.CreateProjectAsync(request);

        if (result.IsSuccess)
        {
            AnsiConsole.MarkupLine(I18n.T("ProjectInit.Success", projectName!.EscapeMarkup(), result.Value?.ProjectRootPath?.EscapeMarkup() ?? string.Empty));
            if (existingSessionId != null)
            {
                AnsiConsole.MarkupLine(I18n.T("ProjectInit.SwitchTip"));
            }
        }
        else
        {
            AnsiConsole.MarkupLine(I18n.T("ProjectInit.Error", result.Error?.EscapeMarkup() ?? I18n.T("Common.UnknownError")));
        }
    }

    private static async Task<AiProjectContent?> GenerateAiProjectContentAsync(
        IClawRuntime runtime,
        ClawOptions options,
        string idea,
        ProjectTemplateDefinition template,
        SessionId? existingSessionId)
    {
        return await AnsiConsole.Status().StartAsync(I18n.T("ProjectInit.AiThinking"), async ctx =>
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

                          Please generate four things in JSON format:
                          1. "summary": A concise one-sentence summary of the project.
                          2. "appendix": A detailed Markdown section for README covering project goals and high-level roadmap.
                          3. "plan": A detailed initial project implementation plan in Markdown format, following the structure: Background, Goals, Implementation Steps (detailed), Verification Strategy.
                          4. "spec": A technical specification for the initial research/feature in Markdown format.

                          Response MUST be a valid JSON object with "summary", "appendix", "plan", and "spec" keys.
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
                        doc.RootElement.GetProperty("appendix").GetString() ?? "",
                        doc.RootElement.GetProperty("plan").GetString() ?? "",
                        doc.RootElement.GetProperty("spec").GetString() ?? ""
                    );
                }
            }
            catch
            {
                return new AiProjectContent(idea, result.AssistantMessage, "", "");
            }

            return null;
        });
    }
}
