using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using ClawSharp.CLI.Infrastructure;
using Spectre.Console;

namespace ClawSharp.CLI.Commands;

public static partial class ChatCommand
{
    private static async Task<CommandDispatchResult> HandlePasteAsync()
    {
        AnsiConsole.MarkupLine(I18n.T("Chat.Paste.Mode"));
        var pastedContent = await MultilineInputCollector.CapturePasteAsync(ReadPasteLineAsync, "[bold magenta]Paste[/] > ");
        return string.IsNullOrWhiteSpace(pastedContent)
            ? CommandDispatchResult.Handled()
            : CommandDispatchResult.Submit(pastedContent);
    }

    private static async Task<CommandDispatchResult> HandleEditAsync()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"clawsharp-edit-{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(tempFile, string.Empty);

        try
        {
            var editor = Environment.GetEnvironmentVariable("EDITOR");
            if (string.IsNullOrWhiteSpace(editor))
            {
                editor = OperatingSystem.IsWindows() ? "notepad" : "vi";
            }

            using var process = Process.Start(CreateEditorStartInfo(editor, tempFile))
                ?? throw new InvalidOperationException(I18n.T("Chat.Edit.StartFailed", editor));
            await process.WaitForExitAsync();

            var editedContent = await File.ReadAllTextAsync(tempFile);
            if (string.IsNullOrWhiteSpace(editedContent))
            {
                AnsiConsole.MarkupLine(I18n.T("Chat.Edit.NoContent"));
                return CommandDispatchResult.Handled();
            }

            return CommandDispatchResult.Submit(editedContent.Trim());
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    internal static ProcessStartInfo CreateEditorStartInfo(string editorCommand, string filePath)
    {
        if (OperatingSystem.IsWindows())
        {
            return new ProcessStartInfo("cmd.exe", $"/c {editorCommand} \"{filePath}\"")
            {
                UseShellExecute = false
            };
        }

        return new ProcessStartInfo("/bin/sh")
        {
            UseShellExecute = false,
            RedirectStandardInput = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            ArgumentList = { "-lc", $"{editorCommand} {EscapeShellArgument(filePath)}" }
        };
    }

    private static async Task<string?> ReadPasteLineAsync(string promptMarkup)
    {
        AnsiConsole.Markup(promptMarkup);
        return await Task.FromResult(Console.ReadLine());
    }

    private static string EscapeShellArgument(string value) =>
        $"'{value.Replace("'", "'\\''", StringComparison.Ordinal)}'";

    private static class InputPreprocessor
    {
        private static readonly Regex FileRefRegex = new(@"@(\S+)", RegexOptions.Compiled);

        public static async Task<string> ProcessAsync(string input, string? currentDirectory)
        {
            if (string.IsNullOrWhiteSpace(input) || !input.Contains("@"))
            {
                return input;
            }

            var baseDir = currentDirectory ?? Directory.GetCurrentDirectory();
            var matches = FileRefRegex.Matches(input);
            if (matches.Count == 0)
            {
                return input;
            }

            var sb = new StringBuilder(input);
            var offset = 0;

            foreach (Match match in matches)
            {
                var relativePath = match.Groups[1].Value;
                var fullPath = Path.GetFullPath(Path.Combine(baseDir, relativePath));

                if (File.Exists(fullPath))
                {
                    try
                    {
                        var content = await File.ReadAllTextAsync(fullPath);
                        var ext = Path.GetExtension(fullPath).TrimStart('.');
                        var replacement = $"\n\n[File: {relativePath}]\n```{ext}\n{content}\n```\n";

                        sb.Remove(match.Index + offset, match.Length);
                        sb.Insert(match.Index + offset, replacement);
                        offset += replacement.Length - match.Length;
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine(I18n.T("Chat.Input.WarningReadFile", relativePath.EscapeMarkup(), ex.Message.EscapeMarkup()));
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine(I18n.T("Chat.Input.WarningFileMissing", relativePath.EscapeMarkup()));
                }
            }

            return sb.ToString();
        }
    }
}
