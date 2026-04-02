using Spectre.Console;
using ClawSharp.Lib.Runtime;

namespace ClawSharp.CLI.Infrastructure;

public static class StatsRenderer
{
    public static void RenderSummary(SessionAnalyticsSnapshot snapshot, IReadOnlyList<TokenUsageMetric> trend, string periodLabel)
    {
        if (trend.Count == 0 && snapshot.TotalSessions == 0 && snapshot.MessagesByRole.Count == 0)
        {
            RenderNoData(I18n.T("Stats.NoAnalytics", periodLabel));
            return;
        }

        var totalInput = trend.Sum(x => x.InputTokens);
        var totalOutput = trend.Sum(x => x.OutputTokens);
        var totalMessages = snapshot.MessagesByRole.Sum(x => x.Count);

        var table = CreateTable(I18n.T("Stats.Column.Metric"), I18n.T("Common.Value"));
        table.AddRow(I18n.T("Stats.Column.Period"), periodLabel.EscapeMarkup());
        table.AddRow(I18n.T("Stats.Column.InputTokens"), totalInput.ToString("N0"));
        table.AddRow(I18n.T("Stats.Column.OutputTokens"), totalOutput.ToString("N0"));
        table.AddRow(I18n.T("Stats.Column.TotalTokens"), (totalInput + totalOutput).ToString("N0"));
        table.AddRow(I18n.T("Stats.Column.Sessions"), snapshot.TotalSessions.ToString("N0"));
        table.AddRow(I18n.T("Stats.Column.ActiveSessions"), snapshot.ActiveSessions.ToString("N0"));
        table.AddRow(I18n.T("Stats.Column.Messages"), totalMessages.ToString("N0"));

        AnsiConsole.Write(new Panel(table)
            .Header(I18n.T("Stats.Summary.Header", periodLabel.EscapeMarkup()))
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(Color.Blue)));
    }

    public static void RenderTools(IReadOnlyList<ToolUsageMetric> tools, string periodLabel)
    {
        if (tools.Count == 0)
        {
            RenderNoData(I18n.T("Stats.NoTools", periodLabel));
            return;
        }

        var table = CreateTable(
            I18n.T("Stats.Column.Tool"),
            I18n.T("Stats.Column.Calls"),
            I18n.T("Stats.Column.Success"),
            I18n.T("Stats.Column.Failure"),
            I18n.T("Stats.Column.SuccessRate"));
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
            .Header(I18n.T("Stats.Tools.Header", periodLabel.EscapeMarkup()))
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(Color.Green)));
    }

    public static void RenderAgents(IReadOnlyList<AgentPerformanceMetric> agents, string periodLabel)
    {
        if (agents.Count == 0)
        {
            RenderNoData(I18n.T("Stats.NoAgents", periodLabel));
            return;
        }

        var table = CreateTable(
            I18n.T("Stats.Column.Agent"),
            I18n.T("Stats.Column.AvgMs"),
            I18n.T("Stats.Column.MinMs"),
            I18n.T("Stats.Column.MaxMs"),
            I18n.T("Stats.Column.Turns"));
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
            .Header(I18n.T("Stats.Agents.Header", periodLabel.EscapeMarkup()))
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
