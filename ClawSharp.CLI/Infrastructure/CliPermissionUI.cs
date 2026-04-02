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
        var panel = new Panel(new Markup(I18n.T(
            "Permissions.Capability.Request",
            Environment.NewLine,
            agentId.EscapeMarkup(),
            toolName.EscapeMarkup(),
            capability.ToString().EscapeMarkup())))
        {
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 1, 1, 1)
        };
        AnsiConsole.Write(panel);

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title(I18n.T("Permissions.Capability.Confirm"))
                .AddChoices(I18n.T("Common.Yes"), I18n.T("Common.No")));

        return choice == I18n.T("Common.Yes");
    }

    /// <inheritdoc />
    public async Task<bool> RequestApprovalAsync(string agentId, string toolName, System.Text.Json.JsonElement payload, CancellationToken cancellationToken = default)
    {
        AnsiConsole.WriteLine();
        
        var header = new Rule(I18n.T("Permissions.Approval.Header", toolName.ToUpperInvariant().EscapeMarkup()))
        {
            Justification = Justify.Left
        };
        AnsiConsole.Write(header);

        if (toolName.Equals("shell_run", StringComparison.OrdinalIgnoreCase))
        {
            var command = payload.GetProperty("command").GetString() ?? string.Empty;
            AnsiConsole.Write(new Panel(new Text(command, new Style(Color.Grey))) 
            { 
                Header = new PanelHeader(I18n.T("Permissions.Approval.CommandPanel")),
                Border = BoxBorder.Rounded 
            });
        }
        else if (toolName.Equals("file_write", StringComparison.OrdinalIgnoreCase))
        {
            var path = payload.GetProperty("path").GetString() ?? string.Empty;
            var content = payload.TryGetProperty("content", out var c)
                ? c.GetString() ?? string.Empty
                : I18n.T("Permissions.Approval.ContentUnavailable");
            
            AnsiConsole.Write(new Text($"{I18n.T("Permissions.Approval.TargetPath", path)}\n", new Style(Color.Blue)));
            
            AnsiConsole.Write(new Panel(new Text(content.Length > 500 ? content[..500] + "\n..." : content))
            {
                Header = new PanelHeader(I18n.T("Permissions.Approval.ContentPanel")),
                Border = BoxBorder.Rounded
            });
        }
        else
        {
            AnsiConsole.Write(new Text(payload.GetRawText(), new Style(Color.Grey)));
        }

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title(I18n.T("Permissions.Approval.Confirm", agentId.EscapeMarkup()))
                .AddChoices(
                    I18n.T("Permissions.Approval.Allow"),
                    I18n.T("Permissions.Approval.Deny"),
                    I18n.T("Permissions.Approval.AlwaysAllow")));

        if (choice == I18n.T("Permissions.Approval.AlwaysAllow"))
        {
            // Note: In a real implementation, we would add this to a session-level whitelist
            // For now, "Allow" and "Always Allow" both return true, but ClawRuntime already handles whitelisting for the current context.
            return true;
        }

        return choice == I18n.T("Permissions.Approval.Allow");
    }
}
