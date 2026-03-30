using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClawSharp.Lib.Configuration;
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
        var templates = bootstrapper.GetProviderTemplates().ToList();

        var config = new BootstrapConfig();

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
                .UseConverter(t => t.Name));

        config.DefaultProvider = selectedTemplate.Id;
        config.ProviderType = selectedTemplate.Type;

        // 4. API Key (FR-006)
        if (selectedTemplate.Type != "stub")
        {
            config.ApiKey = AnsiConsole.Prompt(
                new TextPrompt<string>($"Enter [green]API Key[/] for {selectedTemplate.Name}:")
                    .PromptStyle("red")
                    .Secret());
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

        return true;
    }
}
