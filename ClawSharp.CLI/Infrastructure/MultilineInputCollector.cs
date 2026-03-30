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
}
