using Spectre.Console;
using ClawSharp.Lib.Runtime;

namespace ClawSharp.CLI.Infrastructure;

public static class StatsRenderer
{
    public static void RenderSummary(SessionAnalyticsSnapshot snapshot, IReadOnlyList<TokenUsageMetric> trend, string periodLabel)
    {
        if (trend.Count == 0 && snapshot.TotalSessions == 0 && snapshot.MessagesByRole.Count == 0)
        {
            RenderNoData($"No analytics data found for {periodLabel}.");
            return;
        }

        var totalInput = trend.Sum(x => x.InputTokens);
        var totalOutput = trend.Sum(x => x.OutputTokens);
        var totalMessages = snapshot.MessagesByRole.Sum(x => x.Count);

        var table = CreateTable("Metric", "Value");
        table.AddRow("Period", periodLabel.EscapeMarkup());
        table.AddRow("Input Tokens", totalInput.ToString("N0"));
        table.AddRow("Output Tokens", totalOutput.ToString("N0"));
        table.AddRow("Total Tokens", (totalInput + totalOutput).ToString("N0"));
        table.AddRow("Sessions", snapshot.TotalSessions.ToString("N0"));
        table.AddRow("Active Sessions", snapshot.ActiveSessions.ToString("N0"));
        table.AddRow("Messages", totalMessages.ToString("N0"));

        AnsiConsole.Write(new Panel(table)
            .Header($"[bold]claw stats[/] [grey]({periodLabel.EscapeMarkup()})[/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(Color.Blue)));
    }

    public static void RenderTools(IReadOnlyList<ToolUsageMetric> tools, string periodLabel)
    {
        if (tools.Count == 0)
        {
            RenderNoData($"No tool usage found for {periodLabel}.");
            return;
        }

        var table = CreateTable("Tool", "Calls", "Success", "Failure", "Success %");
        foreach (var tool in tools)
        {
            var successRate = tool.CallCount == 0
                ? 0
                : (double)tool.SuccessCount / tool.CallCount * 100;
            table.AddRow(
                tool.ToolName.EscapeMarkup(),
                tool.CallCount.ToString("N0"),
                tool.SuccessCount.ToString("N0"),
                tool.FailureCount.ToString("N0"),
                $"{successRate:0.#}%");
        }

        AnsiConsole.Write(new Panel(table)
            .Header($"[bold]Tool Usage[/] [grey]({periodLabel.EscapeMarkup()})[/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(Color.Green)));
    }

    public static void RenderAgents(IReadOnlyList<AgentPerformanceMetric> agents, string periodLabel)
    {
        if (agents.Count == 0)
        {
            RenderNoData($"No agent performance data found for {periodLabel}.");
            return;
        }

        var table = CreateTable("Agent", "Avg ms", "Min ms", "Max ms", "Turns");
        foreach (var agent in agents)
        {
            table.AddRow(
                agent.AgentId.EscapeMarkup(),
                agent.AvgLatencyMs.ToString("0.0"),
                agent.MinLatencyMs.ToString("0.0"),
                agent.MaxLatencyMs.ToString("0.0"),
                agent.RequestCount.ToString("N0"));
        }

        AnsiConsole.Write(new Panel(table)
            .Header($"[bold]Agent Performance[/] [grey]({periodLabel.EscapeMarkup()})[/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(Color.Yellow)));
    }

    public static void RenderNoData(string message)
    {
        AnsiConsole.Write(new Panel($"[grey]{message.EscapeMarkup()}[/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(Color.Grey)));
    }

    private static Table CreateTable(params string[] columns)
    {
        var table = new Table().Border(TableBorder.Rounded).Expand();
        foreach (var column in columns)
        {
            table.AddColumn(new TableColumn(column));
        }

        return table;
    }
}
