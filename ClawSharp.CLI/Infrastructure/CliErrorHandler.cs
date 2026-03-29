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
