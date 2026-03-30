using ClawSharp.Lib.Runtime;
using ClawSharp.Lib.Tools;
using Spectre.Console;

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
}
