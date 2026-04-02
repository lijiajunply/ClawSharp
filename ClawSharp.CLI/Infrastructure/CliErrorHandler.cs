using Spectre.Console;
using ClawSharp.Lib.Core;

namespace ClawSharp.CLI.Infrastructure;

public static class CliErrorHandler
{
    public static void Handle(Exception ex)
    {
        if (ex is ValidationException validationEx)
        {
            AnsiConsole.MarkupLine(I18n.T("Errors.Validation", validationEx.Message.EscapeMarkup()));
        }
        else if (ex is EnvironmentDependencyException envEx)
        {
            AnsiConsole.MarkupLine(I18n.T("Errors.Environment", envEx.Message.EscapeMarkup()));
            if (!string.IsNullOrEmpty(envEx.FixCommand))
            {
                AnsiConsole.WriteLine();
                var panel = new Panel(new Text(envEx.FixCommand, new Style(Color.Green)))
                {
                    Header = new PanelHeader(I18n.T("Errors.RecommendedFix")),
                    Border = BoxBorder.Rounded,
                    Padding = new Padding(1, 0, 1, 0)
                };
                AnsiConsole.Write(panel);
                AnsiConsole.MarkupLine(I18n.T("Errors.RunFix"));
            }
        }
        else
        {
            AnsiConsole.MarkupLine(I18n.T("Errors.Generic", ex.Message.EscapeMarkup()));
            if (AnsiConsole.Confirm(I18n.T("Errors.ShowStackTrace")))
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
