using Spectre.Console;

namespace ClawSharp.CLI.Infrastructure;

public static class ThemeConfig
{
    public static Color UserColor => Color.Green;
    public static Color AgentColor => Color.Blue;
    public static Color ErrorColor => Color.Red;
    public static Color InfoColor => Color.Grey;

    public static string UserPrefix => $"[bold {UserColor.ToMarkup()}]User >[/]";
    public static string AgentPrefix => $"[bold {AgentColor.ToMarkup()}]Agent >[/]";
}
