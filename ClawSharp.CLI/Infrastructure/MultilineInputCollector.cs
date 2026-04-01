using Spectre.Console;

namespace ClawSharp.CLI.Infrastructure;

internal static class MultilineInputCollector
{
    internal static async Task<string> CapturePasteAsync(
        Func<string, Task<string?>> readLineAsync,
        string promptMarkup,
        CancellationToken cancellationToken = default)
    {
        var lines = new List<string>();

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await readLineAsync(promptMarkup).ConfigureAwait(false);
            if (line is null || line == ".")
            {
                break;
            }

            lines.Add(line);
        }

        return Compose(lines);
    }

    internal static string Compose(IEnumerable<string> lines) =>
        string.Join(Environment.NewLine, lines);

    internal static string Collect(string prompt)
    {
        AnsiConsole.MarkupLine($"[grey]{prompt.EscapeMarkup()}[/]");
        var lines = new List<string>();
        while (true)
        {
            var line = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(line)) break;
            lines.Add(line);
        }
        return string.Join(Environment.NewLine, lines);
    }
}
