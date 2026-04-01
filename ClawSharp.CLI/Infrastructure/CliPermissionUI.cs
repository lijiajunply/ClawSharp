using ClawSharp.Lib.Runtime;
using ClawSharp.Lib.Tools;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace ClawSharp.CLI.Infrastructure;

/// <summary>
/// CLI 终端下的权限交互实现。
/// </summary>
public sealed class CliPermissionUI : IPermissionUI
{
    /// <inheritdoc />
    public async Task<bool> RequestCapabilityAsync(string agentId, ToolCapability capability, string toolName, CancellationToken cancellationToken = default)
    {
        AnsiConsole.WriteLine();
        var panel = new Panel(new Markup($"[bold yellow]PERMISSION REQUEST[/]\nAgent [green]{agentId.EscapeMarkup()}[/] is attempting to use [blue]{toolName.EscapeMarkup()}[/] which requires [red]{capability}[/].\nThis capability is not currently granted to the agent."))
        {
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 1, 1, 1)
        };
        AnsiConsole.Write(panel);

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Do you want to grant this capability for the current session?")
                .AddChoices("Yes", "No"));

        return choice == "Yes";
    }

    /// <inheritdoc />
    public async Task<bool> RequestApprovalAsync(string agentId, string toolName, System.Text.Json.JsonElement payload, CancellationToken cancellationToken = default)
    {
        AnsiConsole.WriteLine();
        
        var header = new Rule($"[bold yellow]APPROVAL REQUIRED: {toolName.ToUpper()}[/]") { Justification = Justify.Left };
        AnsiConsole.Write(header);

        if (toolName.Equals("shell_run", StringComparison.OrdinalIgnoreCase))
        {
            var command = payload.GetProperty("command").GetString() ?? string.Empty;
            AnsiConsole.Write(new Panel(new Text(command, new Style(Color.Grey))) 
            { 
                Header = new PanelHeader("Proposed Command"),
                Border = BoxBorder.Rounded 
            });
        }
        else if (toolName.Equals("file_write", StringComparison.OrdinalIgnoreCase))
        {
            var path = payload.GetProperty("path").GetString() ?? string.Empty;
            var content = payload.TryGetProperty("content", out var c) ? c.GetString() ?? string.Empty : "(Content not available)";
            
            AnsiConsole.Write(new Text($"Target Path: {path}\n", new Style(Color.Blue)));
            
            AnsiConsole.Write(new Panel(new Text(content.Length > 500 ? content[..500] + "\n..." : content))
            {
                Header = new PanelHeader("File Content Preview"),
                Border = BoxBorder.Rounded
            });
        }
        else
        {
            AnsiConsole.Write(new Text(payload.GetRawText(), new Style(Color.Grey)));
        }

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"Allow Agent [green]{agentId.EscapeMarkup()}[/] to proceed?")
                .AddChoices("Allow", "Deny", "Always Allow (Session)"));

        if (choice == "Always Allow (Session)")
        {
            // Note: In a real implementation, we would add this to a session-level whitelist
            // For now, "Allow" and "Always Allow" both return true, but ClawRuntime already handles whitelisting for the current context.
            return true;
        }

        return choice == "Allow";
    }
}
