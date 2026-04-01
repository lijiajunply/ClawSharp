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
            AnsiConsole.MarkupLine("[yellow]Warning:[/] [blue]appsettings.json[/] is missing, but [blue]appsettings.Local.json[/] was found.");
            if (!AnsiConsole.Confirm("Do you want to use the existing local configuration and skip setup?"))
            {
                AnsiConsole.MarkupLine("[grey]Proceeding with fresh configuration...[/]");
            }
            else
            {
                return true;
            }
        }

        AnsiConsole.Write(new FigletText("ClawSharp").Color(Color.Blue));
        AnsiConsole.MarkupLine("Welcome to [blue]ClawSharp[/]! Let's get you set up with a new configuration.");
        AnsiConsole.WriteLine();

        var bootstrapper = new ConfigBootstrapper();
        var discovery = await EnvironmentDiscoveryInspector.DiscoverAsync();
        var templates = bootstrapper.GetProviderTemplates(discovery).ToList();

        var config = new BootstrapConfig();

        if (discovery.HasLocalModelProvider)
        {
            AnsiConsole.MarkupLine("[bold green]Detected local model services:[/]");
            if (discovery.Ollama.Available)
            {
                var modelSummary = discovery.Ollama.Models.Count == 0
                    ? "no models reported"
                    : string.Join(", ", discovery.Ollama.Models.Take(3));
                AnsiConsole.MarkupLine($"[grey]- Ollama:[/] {discovery.Ollama.BaseUrl?.EscapeMarkup()} ({modelSummary.EscapeMarkup()})");
            }

            if (discovery.LlamaEdge.Available)
            {
                var modelSummary = discovery.LlamaEdge.Models.Count == 0
                    ? "no models reported"
                    : string.Join(", ", discovery.LlamaEdge.Models.Take(3));
                AnsiConsole.MarkupLine($"[grey]- LlamaEdge:[/] {discovery.LlamaEdge.BaseUrl?.EscapeMarkup()} ({modelSummary.EscapeMarkup()})");
            }

            AnsiConsole.WriteLine();
        }

        // 1. Workspace Root (FR-003)
        config.WorkspaceRoot = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter [green]workspace root[/] path:")
                .DefaultValue(".")
                .Validate(path => 
                {
                    try { Path.GetFullPath(path); return ValidationResult.Success(); }
                    catch { return ValidationResult.Error("[red]Invalid path format.[/]"); }
                }));

        // 2. Data Path (FR-004)
        config.DataPath = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter [green]runtime data[/] directory name:")
                .DefaultValue(".clawsharp"));

        // 3. Provider Selection (FR-005, US3)
        var selectedTemplate = AnsiConsole.Prompt(
            new SelectionPrompt<ProviderTemplate>()
                .Title("Select your [green]default AI provider[/]:")
                .PageSize(10)
                .AddChoices(templates)
                .UseConverter(t =>
                {
                    var detected = !t.RequiresApiKey && !string.IsNullOrWhiteSpace(t.BaseUrl) ? " [green](Detected)[/]" : string.Empty;
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
                new TextPrompt<string>($"Enter [green]API Key[/] for {selectedTemplate.Name}:")
                    .PromptStyle("red")
                    .Secret());
        }

        if (string.IsNullOrWhiteSpace(config.DefaultModel) && selectedTemplate.Id is "ollama-local" or "llamaedge-local")
        {
            config.DefaultModel = AnsiConsole.Prompt(
                new TextPrompt<string>($"Enter [green]default model[/] for {selectedTemplate.Name}:")
                    .Validate(value => string.IsNullOrWhiteSpace(value)
                        ? ValidationResult.Error("[red]Model name is required for local providers.[/]")
                        : ValidationResult.Success()));
        }

        // 5. Generate and Save (FR-007)
        await AnsiConsole.Status()
            .StartAsync("Generating [blue]appsettings.json[/]...", async ctx =>
            {
                var json = bootstrapper.GenerateConfigJson(config);
                await bootstrapper.SaveConfigAsync(primaryJson, json);
                await Task.Delay(500); // Give user time to see the status
            });

        AnsiConsole.MarkupLine("[bold green]Configuration generated successfully![/]");
        AnsiConsole.WriteLine();

        // 6. Optional: browser dependency installation (for web_browser tool)
        if (AnsiConsole.Confirm("Would you like to install [green]Playwright[/] browser drivers now? (Required for [blue]web_browser[/] tool)"))
        {
            await AnsiConsole.Status()
                .StartAsync("Installing [blue]Playwright[/] browser drivers...", async ctx =>
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
                                AnsiConsole.MarkupLine("[bold green]Playwright browsers installed successfully![/]");
                            }
                            else
                            {
                                AnsiConsole.MarkupLine("[bold red]Playwright installation failed. Please run the script manually: .specify/scripts/bash/install-playwright.sh[/]");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[bold red]Error running installation script: {ex.Message}[/]");
                    }
                });
        }

        return true;
    }
}
