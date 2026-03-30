using System.Text;
using System.Text.RegularExpressions;
using Spectre.Console;

namespace ClawSharp.CLI.Infrastructure;

public sealed class ReplPrompt
{
    private readonly List<string> _suggestions = new();
    private List<string> _history = new();
    private int _historyIndex = -1;
    private string _currentHistoryInput = string.Empty;
    private string? _historyFilePath;

    public void AddSuggestions(IEnumerable<string> suggestions)
    {
        foreach (var s in suggestions)
        {
            if (string.IsNullOrWhiteSpace(s)) continue;
            if (!_suggestions.Contains(s))
                _suggestions.Add(s);
        }
    }

    public void LoadHistory(string filePath)
    {
        _historyFilePath = filePath;
        if (File.Exists(filePath))
        {
            _history = File.ReadAllLines(filePath).Reverse().ToList();
        }
    }

    private void SaveHistory(string item)
    {
        if (string.IsNullOrWhiteSpace(_historyFilePath)) return;
        
        try
        {
            // Append to file
            File.AppendAllLines(_historyFilePath, new[] { item });
        }
        catch
        {
            // Ignore history save errors
        }
    }

    public async Task<string> AskAsync(string promptMarkup)
    {
        var input = new StringBuilder();
        var cursor = 0;
        
        // Measure prompt length by stripping markup
        var promptPlain = Regex.Replace(promptMarkup, @"\[[^\]]*\]", "");
        var promptLength = promptPlain.Length;

        // Ensure we are at the start of a new line
        if (Console.CursorLeft != 0) AnsiConsole.WriteLine();

        while (true)
        {
            var suggestion = GetSuggestion(input.ToString());
            
            // 1. Move to start of line and clear everything after prompt
            Console.CursorVisible = false;
            Console.SetCursorPosition(0, Console.CursorTop);
            
            // 2. Render Prompt
            AnsiConsole.Markup(promptMarkup);
            
            // 3. Render Input
            var inputStr = input.ToString();
            AnsiConsole.Markup(inputStr.EscapeMarkup());
            
            // 4. Render Ghost Suggestion
            if (!string.IsNullOrEmpty(suggestion) && suggestion.Length > inputStr.Length)
            {
                var ghostText = suggestion.Substring(inputStr.Length);
                AnsiConsole.Markup($"[grey]{ghostText.EscapeMarkup()}[/]");
            }
            
            // 5. Clear tail (if any)
            var currentPos = Console.CursorLeft;
            AnsiConsole.Write(new string(' ', Math.Max(0, Console.WindowWidth - currentPos - 1)));
            
            // 6. Restore cursor position
            Console.SetCursorPosition(promptLength + cursor, Console.CursorTop);
            Console.CursorVisible = true;

            var key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.Enter)
            {
                var result = input.ToString();
                if (!string.IsNullOrWhiteSpace(result))
                {
                    if (_history.Count == 0 || _history[0] != result)
                    {
                        _history.Insert(0, result);
                        SaveHistory(result);
                    }
                }
                _historyIndex = -1;
                AnsiConsole.WriteLine();
                return result;
            }

            if (key.Key == ConsoleKey.Tab || (key.Key == ConsoleKey.RightArrow && cursor == input.Length))
            {
                if (!string.IsNullOrEmpty(suggestion))
                {
                    input.Clear();
                    input.Append(suggestion);
                    cursor = input.Length;
                }
                continue;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (cursor > 0)
                {
                    input.Remove(cursor - 1, 1);
                    cursor--;
                }
                continue;
            }

            if (key.Key == ConsoleKey.Delete)
            {
                if (cursor < input.Length)
                {
                    input.Remove(cursor, 1);
                }
                continue;
            }

            if (key.Key == ConsoleKey.LeftArrow)
            {
                if (cursor > 0) cursor--;
                continue;
            }

            if (key.Key == ConsoleKey.RightArrow)
            {
                if (cursor < input.Length) cursor++;
                continue;
            }

            if (key.Key == ConsoleKey.UpArrow)
            {
                if (_history.Count > 0 && _historyIndex < _history.Count - 1)
                {
                    if (_historyIndex == -1) _currentHistoryInput = input.ToString();
                    _historyIndex++;
                    input.Clear();
                    input.Append(_history[_historyIndex]);
                    cursor = input.Length;
                }
                continue;
            }

            if (key.Key == ConsoleKey.DownArrow)
            {
                if (_historyIndex > 0)
                {
                    _historyIndex--;
                    input.Clear();
                    input.Append(_history[_historyIndex]);
                    cursor = input.Length;
                }
                else if (_historyIndex == 0)
                {
                    _historyIndex = -1;
                    input.Clear();
                    input.Append(_currentHistoryInput);
                    cursor = input.Length;
                }
                continue;
            }

            if (key.Key == ConsoleKey.U && key.Modifiers.HasFlag(ConsoleModifiers.Control))
            {
                input.Clear();
                cursor = 0;
                continue;
            }

            if (key.Key == ConsoleKey.Escape)
            {
                input.Clear();
                cursor = 0;
                continue;
            }

            if (key.KeyChar != '\0' && !char.IsControl(key.KeyChar))
            {
                input.Insert(cursor, key.KeyChar);
                cursor++;
            }
        }
    }

    private string GetSuggestion(string currentInput)
    {
        if (string.IsNullOrWhiteSpace(currentInput)) return string.Empty;

        return _suggestions
            .Where(s => s.StartsWith(currentInput, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Length)
            .FirstOrDefault() ?? string.Empty;
    }
}
