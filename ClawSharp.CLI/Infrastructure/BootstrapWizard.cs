using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Runtime;
using Spectre.Console;

namespace ClawSharp.CLI.Infrastructure;

/// <summary>
/// 负责在首次启动时引导用户进行基础配置。
/// </summary>
public static class BootstrapWizard
{
    /// <summary>
    /// 运行引导向导。
    /// </summary>
    /// <returns>如果配置成功生成或决定跳过，则返回 true。</returns>
    public static async Task<bool> RunAsync()
    {
        var primaryJson = "appsettings.json";
        var localJson = "appsettings.Local.json";

        if (File.Exists(primaryJson))
        {
            return true;
        }

        // 检查是否存在 Local 配置 (FR-009)
        if (File.Exists(localJson))
        {
            AnsiConsole.MarkupLine(I18n.T("Bootstrap.WarningExistingLocal"));
            if (!AnsiConsole.Confirm(I18n.T("Bootstrap.ConfirmUseLocal")))
            {
                AnsiConsole.MarkupLine(I18n.T("Bootstrap.FreshConfig"));
            }
            else
            {
                return true;
            }
        }

        AnsiConsole.Write(new FigletText("ClawSharp").Color(Color.Blue));
        AnsiConsole.MarkupLine(I18n.T("Bootstrap.Welcome"));
        AnsiConsole.WriteLine();

        var bootstrapper = new ConfigBootstrapper();
        var discovery = await EnvironmentDiscoveryInspector.DiscoverAsync();
        var templates = bootstrapper.GetProviderTemplates(discovery).ToList();

        var config = new BootstrapConfig();

        if (discovery.HasLocalModelProvider)
        {
            AnsiConsole.MarkupLine(I18n.T("Bootstrap.DetectedServices"));
            if (discovery.Ollama.Available)
            {
                var modelSummary = discovery.Ollama.Models.Count == 0
                    ? I18n.T("Bootstrap.NoModelsReported")
                    : string.Join(", ", discovery.Ollama.Models.Take(3));
                AnsiConsole.MarkupLine($"[grey]- Ollama:[/] {discovery.Ollama.BaseUrl?.EscapeMarkup()} ({modelSummary.EscapeMarkup()})");
            }

            if (discovery.LlamaEdge.Available)
            {
                var modelSummary = discovery.LlamaEdge.Models.Count == 0
                    ? I18n.T("Bootstrap.NoModelsReported")
                    : string.Join(", ", discovery.LlamaEdge.Models.Take(3));
                AnsiConsole.MarkupLine($"[grey]- LlamaEdge:[/] {discovery.LlamaEdge.BaseUrl?.EscapeMarkup()} ({modelSummary.EscapeMarkup()})");
            }

            AnsiConsole.WriteLine();
        }

        // 1. Workspace Root (FR-003)
        config.WorkspaceRoot = AnsiConsole.Prompt(
            new TextPrompt<string>(I18n.T("Bootstrap.WorkspaceRootPrompt"))
                .DefaultValue(".")
                .Validate(path => 
                {
                    try { Path.GetFullPath(path); return ValidationResult.Success(); }
                    catch { return ValidationResult.Error(I18n.T("Bootstrap.InvalidPath")); }
                }));

        // 2. Data Path (FR-004)
        config.DataPath = AnsiConsole.Prompt(
            new TextPrompt<string>(I18n.T("Bootstrap.DataPathPrompt"))
                .DefaultValue(".clawsharp"));

        // 3. Provider Selection (FR-005, US3)
        var selectedTemplate = AnsiConsole.Prompt(
            new SelectionPrompt<ProviderTemplate>()
                .Title(I18n.T("Bootstrap.ProviderSelectTitle"))
                .PageSize(10)
                .AddChoices(templates)
                .UseConverter(t =>
                {
                    var detected = !t.RequiresApiKey && !string.IsNullOrWhiteSpace(t.BaseUrl) ? I18n.T("Bootstrap.ProviderDetected") : string.Empty;
                    return $"{t.Name}{detected}";
                }));

        config.DefaultProvider = selectedTemplate.Id;
        config.ProviderType = selectedTemplate.Type;
        config.BaseUrl = selectedTemplate.BaseUrl;
        config.DefaultModel = selectedTemplate.DefaultModel;
        config.RequestPath = selectedTemplate.RequestPath;
        config.SupportsResponses = selectedTemplate.SupportsResponses;
        config.SupportsChatCompletions = selectedTemplate.SupportsChatCompletions;

        // 4. API Key (FR-006)
        if (selectedTemplate.RequiresApiKey)
        {
            config.ApiKey = AnsiConsole.Prompt(
                new TextPrompt<string>(I18n.T("Bootstrap.ApiKeyPrompt", selectedTemplate.Name))
                    .PromptStyle("red")
                    .Secret());
        }

        if (string.IsNullOrWhiteSpace(config.DefaultModel) && selectedTemplate.Id is "ollama-local" or "llamaedge-local")
        {
            config.DefaultModel = AnsiConsole.Prompt(
                new TextPrompt<string>(I18n.T("Bootstrap.DefaultModelPrompt", selectedTemplate.Name))
                    .Validate(value => string.IsNullOrWhiteSpace(value)
                        ? ValidationResult.Error(I18n.T("Bootstrap.ModelRequired"))
                        : ValidationResult.Success()));
        }

        // 5. Generate and Save (FR-007)
        await AnsiConsole.Status()
            .StartAsync(I18n.T("Bootstrap.GeneratingConfig"), async ctx =>
            {
                var json = bootstrapper.GenerateConfigJson(config);
                await bootstrapper.SaveConfigAsync(primaryJson, json);
                await Task.Delay(500); // Give user time to see the status
            });

        AnsiConsole.MarkupLine(I18n.T("Bootstrap.Generated"));
        AnsiConsole.WriteLine();

        // 6. Optional: browser dependency installation (for web_browser tool)
        if (AnsiConsole.Confirm(I18n.T("Bootstrap.InstallPlaywrightConfirm")))
        {
            await AnsiConsole.Status()
                .StartAsync(I18n.T("Bootstrap.InstallPlaywrightProgress"), async ctx =>
                {
                    try
                    {
                        var scriptPath = Path.Combine("workspace", "scripts", "install-playwright.sh");
                        if (File.Exists(scriptPath))
                        {
                            using var process = new System.Diagnostics.Process();
                            process.StartInfo = new System.Diagnostics.ProcessStartInfo("bash", scriptPath)
                            {
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true
                            };
                            process.Start();
                            await process.WaitForExitAsync();
                            if (process.ExitCode == 0)
                            {
                                AnsiConsole.MarkupLine(I18n.T("Bootstrap.InstallPlaywrightSuccess"));
                            }
                            else
                            {
                                AnsiConsole.MarkupLine(I18n.T("Bootstrap.InstallPlaywrightFailure", scriptPath.EscapeMarkup()));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine(I18n.T("Bootstrap.InstallPlaywrightError", ex.Message.EscapeMarkup()));
                    }
                });
        }

        return true;
    }
}
