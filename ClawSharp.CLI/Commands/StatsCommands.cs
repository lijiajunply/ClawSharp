using System.CommandLine;
using System.Text.Json;
using ClawSharp.CLI.Infrastructure;
using ClawSharp.Lib.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;

namespace ClawSharp.CLI.Commands;

public static class StatsCommands
{
    public static Command Create(IHost host)
    {
        var command = new Command("stats", I18n.T("Stats.Description"));
        command.AddAlias("usage");
        command.AddAlias("metrics");

        var periodOption = new Option<string>("--period", () => "24h", I18n.T("Stats.Option.Period"));
        var toolsOption = new Option<bool>("--tools", I18n.T("Stats.Option.Tools"));
        var agentsOption = new Option<bool>("--agents", I18n.T("Stats.Option.Agents"));
        var formatOption = new Option<string>("--format", () => "table", I18n.T("Stats.Option.Format"));

        command.AddOption(periodOption);
        command.AddOption(toolsOption);
        command.AddOption(agentsOption);
        command.AddOption(formatOption);

        command.SetHandler(async (period, tools, agents, format) =>
        {
            await RunAsync(host, period, tools, agents, format);
        }, periodOption, toolsOption, agentsOption, formatOption);

        return command;
    }

    public static async Task<int> RunAsync(IHost host, string period, bool toolsOnly, bool agentsOnly, string format)
    {
        return await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
        {
            var runtime = host.Services.GetRequiredService<IClawRuntime>();
            await runtime.InitializeAsync().ConfigureAwait(false);

            var analytics = host.Services.GetRequiredService<ISessionAnalyticsService>();
            var range = ParsePeriod(period);
            var normalizedFormat = NormalizeFormat(format);

            var snapshot = await analytics.GetSnapshotAsync(range.Start, range.End).ConfigureAwait(false);
            var tokenTrend = await analytics.GetTokenUsageTrendAsync(range.Start, range.End).ConfigureAwait(false);
            var toolStats = toolsOnly || normalizedFormat == "json"
                ? await analytics.GetToolUsageStatsAsync(range.Start, range.End).ConfigureAwait(false)
                : [];
            var agentStats = agentsOnly || normalizedFormat == "json"
                ? await analytics.GetAgentPerformanceAsync(range.Start, range.End).ConfigureAwait(false)
                : [];

            if (normalizedFormat == "json")
            {
                var payload = new
                {
                    period = range.Label,
                    start = range.Start,
                    end = range.End,
                    summary = snapshot,
                    tokens = tokenTrend,
                    tools = toolStats,
                    agents = agentStats
                };

                AnsiConsole.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
                return 0;
            }

            if (!toolsOnly && !agentsOnly)
            {
                StatsRenderer.RenderSummary(snapshot, tokenTrend, range.Label);
            }

            if (toolsOnly)
            {
                StatsRenderer.RenderTools(toolStats, range.Label);
            }

            if (agentsOnly)
            {
                if (toolsOnly)
                {
                    AnsiConsole.WriteLine();
                }

                StatsRenderer.RenderAgents(agentStats, range.Label);
            }

            return 0;
        });
    }

    private static StatsRange ParsePeriod(string rawPeriod)
    {
        var normalized = string.IsNullOrWhiteSpace(rawPeriod)
            ? "24h"
            : rawPeriod.Trim().ToLowerInvariant();
        var end = DateTimeOffset.UtcNow;

        if (normalized == "all")
        {
            return new StatsRange(DateTimeOffset.MinValue, end, I18n.T("Stats.AllTime"));
        }

        if (normalized.EndsWith("h", StringComparison.Ordinal)
            && int.TryParse(normalized[..^1], out var hours)
            && hours > 0)
        {
            return new StatsRange(end.AddHours(-hours), end, $"{hours}h");
        }

        if (normalized.EndsWith("d", StringComparison.Ordinal)
            && int.TryParse(normalized[..^1], out var days)
            && days > 0)
        {
            return new StatsRange(end.AddDays(-days), end, $"{days}d");
        }

        throw new ArgumentException(I18n.T("Stats.UnsupportedPeriod", rawPeriod));
    }

    private static string NormalizeFormat(string format)
    {
        var normalized = string.IsNullOrWhiteSpace(format)
            ? "table"
            : format.Trim().ToLowerInvariant();

        return normalized switch
        {
            "table" or "json" => normalized,
            _ => throw new ArgumentException(I18n.T("Stats.UnsupportedFormat", format))
        };
    }

    private readonly record struct StatsRange(DateTimeOffset Start, DateTimeOffset End, string Label);
}
