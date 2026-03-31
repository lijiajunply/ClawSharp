using Spectre.Console;
using ClawSharp.Lib.Core;

namespace ClawSharp.CLI.Infrastructure;

public static class CliErrorHandler
{
    public static void Handle(Exception ex)
    {
        if (ex is ValidationException validationEx)
        {
            AnsiConsole.MarkupLine($"[red]Validation Error:[/] {validationEx.Message.EscapeMarkup()}");
        }
        else if (ex is EnvironmentDependencyException envEx)
        {
            AnsiConsole.MarkupLine($"[bold red]Environment Error:[/] {envEx.Message.EscapeMarkup()}");
            if (!string.IsNullOrEmpty(envEx.FixCommand))
            {
                AnsiConsole.WriteLine();
                var panel = new Panel(new Text(envEx.FixCommand, new Style(Color.Green)))
                {
                    Header = new PanelHeader("Recommended Fix"),
                    Border = BoxBorder.Rounded,
                    Padding = new Padding(1, 0, 1, 0)
                };
                AnsiConsole.Write(panel);
                AnsiConsole.MarkupLine("[grey]Please run the command above to install missing dependencies.[/]");
            }
        }
        else
        {
            AnsiConsole.MarkupLine($"[bold red]Error:[/] {ex.Message.EscapeMarkup()}");
            if (AnsiConsole.Confirm("Show stack trace?"))
            {
                AnsiConsole.WriteException(ex);
            }
        }
    }

    public static async Task<int> ExecuteWithHandlingAsync(Func<Task<int>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            Handle(ex);
            return 1;
        }
    }
}
