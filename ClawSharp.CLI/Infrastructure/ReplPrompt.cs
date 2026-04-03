using System.Text;
using System.Text.RegularExpressions;
using Spectre.Console;

namespace ClawSharp.CLI.Infrastructure;

public sealed class ReplPrompt
{
    private static readonly string[] DefaultSuggestions =
    [
        "/help",
        "/new",
        "/resume",
        "/sessions",
        "/agents",
        "/skills",
        "/tools",
        "/config",
        "/history",
        "/stats",
        "/spaces",
        "/hub",
        "/paste",
        "/edit",
        "/cd",
        "/home",
        "/clear",
        "/init",
        "/init-proj",
        "/reload",
        "/speckit",
        "/plan",
        "/quit",
        "/exit"
    ];

    private readonly List<string> _suggestions = new();
    private List<string> _history = new();
    private int _historyIndex = -1;
    private string _currentHistoryInput = string.Empty;
    private string? _historyFilePath;

    // Menu state
    private int _menuSelectionIndex = 0;
    private int _menuStartIndex = 0;
    private List<string> _currentMatches = new();
    private int _lastMenuLineCount = 0;
    private const int MaxVisibleMenuLines = 8;

    public string? CurrentDirectory { get; set; }

    public Func<string, string, IEnumerable<string>>? DynamicSuggestionProvider { get; set; }

    public void AddSuggestions(IEnumerable<string> suggestions)
    {
        foreach (var s in suggestions)
        {
            if (string.IsNullOrWhiteSpace(s)) continue;
            if (!_suggestions.Contains(s))
                _suggestions.Add(s);
        }
    }

    public void AddDefaultSuggestions() => AddSuggestions(DefaultSuggestions);

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
        
        var promptPlain = promptMarkup
            .Replace("[[", "\u0001")
            .Replace("]]", "\u0002");
        promptPlain = Regex.Replace(promptPlain, @"\[[^\]]*\]", "");
        promptPlain = promptPlain
            .Replace("\u0001", "[")
            .Replace("\u0002", "]");
        var promptLength = GetDisplayWidth(promptPlain);

        if (Console.CursorLeft != 0) AnsiConsole.WriteLine();

        while (true)
        {
            var currentInput = input.ToString();
            var isCommandMode = currentInput.StartsWith("/", StringComparison.Ordinal);
            
            _currentMatches = GetMatches(currentInput);
            
            // Only show menu if in command mode OR file reference mode and multiple matches
            var isFileRefMode = currentInput.Contains("@");
            var showMenu = (isCommandMode || isFileRefMode) && _currentMatches.Count > 1;
            
            // Ghost suggestion logic
            var suggestion = (isCommandMode && _currentMatches.Count == 1) || (!isCommandMode && _currentMatches.Count > 0)
                ? _currentMatches.FirstOrDefault() ?? string.Empty 
                : string.Empty;

            // Clamp and handle window scrolling
            UpdateMenuSelectionAndWindow();

            // --- RENDER ---
            AnsiConsole.Cursor.Hide();
            
            // 1. Clear previous menu area
            ClearMenuArea();

            // 2. Clear current prompt line and render
            AnsiConsole.Write("\r");
            AnsiConsole.Markup(promptMarkup);
            AnsiConsole.Markup(currentInput.EscapeMarkup());
            
            // 3. Render Ghost Suggestion
            if (!string.IsNullOrEmpty(suggestion) && suggestion.Length > currentInput.Length)
            {
                var ghostText = suggestion.Substring(currentInput.Length);
                AnsiConsole.Markup($"[grey]{ghostText.EscapeMarkup()}[/]");
            }
            
            // 4. Clear tail
            var currentLinePos = Console.CursorLeft;
            AnsiConsole.Write(new string(' ', Math.Max(0, Console.WindowWidth - currentLinePos - 1)));
            
            // 5. Render Menu (below)
            if (showMenu)
            {
                RenderMenu();
            }

            // 6. Restore cursor position
            AnsiConsole.Write("\r");
            var targetPos = promptLength + GetDisplayWidth(currentInput.Substring(0, cursor));
            AnsiConsole.Cursor.MoveRight(targetPos);
            AnsiConsole.Cursor.Show();

            var key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.Enter)
            {
                if (showMenu && _currentMatches.Count > 0)
                {
                    // Select the current match but don't return yet
                    var selected = _currentMatches[_menuSelectionIndex];
                    input.Clear();
                    input.Append(selected);
                    cursor = input.Length;
                    _menuSelectionIndex = 0;
                    _menuStartIndex = 0;
                    continue;
                }

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
                _menuSelectionIndex = 0;
                _menuStartIndex = 0;
                
                ClearMenuArea();
                AnsiConsole.WriteLine();
                return result;
            }

            if (key.Key == ConsoleKey.Tab)
            {
                if (_currentMatches.Count > 0)
                {
                    var selected = showMenu ? _currentMatches[_menuSelectionIndex] : _currentMatches[0];
                    input.Clear();
                    input.Append(selected);
                    cursor = input.Length;
                    _menuSelectionIndex = 0;
                    _menuStartIndex = 0;
                }
                continue;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (cursor > 0)
                {
                    input.Remove(cursor - 1, 1);
                    cursor--;
                    _menuSelectionIndex = 0;
                    _menuStartIndex = 0;
                }
                continue;
            }

            if (key.Key == ConsoleKey.Delete)
            {
                if (cursor < input.Length)
                {
                    input.Remove(cursor, 1);
                    _menuSelectionIndex = 0;
                    _menuStartIndex = 0;
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
                if (cursor < input.Length)
                {
                    cursor++;
                }
                else if (!string.IsNullOrEmpty(suggestion))
                {
                    input.Clear();
                    input.Append(suggestion);
                    cursor = input.Length;
                    _menuSelectionIndex = 0;
                    _menuStartIndex = 0;
                }
                continue;
            }

            if (key.Key == ConsoleKey.UpArrow)
            {
                if (showMenu)
                {
                    _menuSelectionIndex = (_menuSelectionIndex - 1 + _currentMatches.Count) % _currentMatches.Count;
                }
                else if (_history.Count > 0 && _historyIndex < _history.Count - 1)
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
                if (showMenu)
                {
                    _menuSelectionIndex = (_menuSelectionIndex + 1) % _currentMatches.Count;
                }
                else if (_historyIndex > 0)
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

            if (key.Key == ConsoleKey.Escape)
            {
                input.Clear();
                cursor = 0;
                _menuSelectionIndex = 0;
                _menuStartIndex = 0;
                continue;
            }

            if (key.KeyChar != '\0' && !char.IsControl(key.KeyChar))
            {
                input.Insert(cursor, key.KeyChar);
                cursor++;
                _menuSelectionIndex = 0;
                _menuStartIndex = 0;
            }
        }
    }

    private void UpdateMenuSelectionAndWindow()
    {
        if (_currentMatches.Count == 0)
        {
            _menuSelectionIndex = 0;
            _menuStartIndex = 0;
            return;
        }

        if (_menuSelectionIndex >= _currentMatches.Count) 
            _menuSelectionIndex = _currentMatches.Count - 1;
        if (_menuSelectionIndex < 0) 
            _menuSelectionIndex = 0;

        // Window logic
        if (_menuSelectionIndex < _menuStartIndex)
        {
            _menuStartIndex = _menuSelectionIndex;
        }
        else if (_menuSelectionIndex >= _menuStartIndex + MaxVisibleMenuLines)
        {
            _menuStartIndex = _menuSelectionIndex - MaxVisibleMenuLines + 1;
        }
    }

    private List<string> GetMatches(string currentInput)
    {
        if (string.IsNullOrWhiteSpace(currentInput)) return new List<string>();

        // 1. Check for @ file reference
        var lastAtPos = currentInput.LastIndexOf('@');
        if (lastAtPos >= 0)
        {
            var textBeforeAt = currentInput[..lastAtPos];
            if (lastAtPos == 0 || char.IsWhiteSpace(textBeforeAt[^1]))
            {
                var partialPath = currentInput[(lastAtPos + 1)..];
                return GetFileSuggestions(textBeforeAt, partialPath);
            }
        }

        // 2. Check for subcommand suggestions (only if space exists)
        if (currentInput.StartsWith("/") && currentInput.Contains(" "))
        {
            var parts = currentInput.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var cmd = parts[0].ToLowerInvariant();
            var arg = parts.Length > 1 ? parts[1] : string.Empty;

            var subcommands = GetSubcommandSuggestions(cmd);
            var matches = subcommands
                .Where(s => s.StartsWith(arg, StringComparison.OrdinalIgnoreCase))
                .Select(s => $"{cmd} {s}")
                .ToList();

            if (matches.Any()) return matches;

            // 3. Dynamic suggestions (only if subcommands didn't match)
            if (DynamicSuggestionProvider != null)
            {
                var dynamic = DynamicSuggestionProvider(cmd, arg);
                var dynamicMatches = dynamic
                    .Where(s => s.StartsWith(arg, StringComparison.OrdinalIgnoreCase))
                    .Select(s => $"{cmd} {s}")
                    .ToList();
                if (dynamicMatches.Any()) return dynamicMatches;
            }
        }

        // 4. Default command suggestions (Matches /s to /sessions, /spaces etc)
        return _suggestions
            .Where(s => s.StartsWith(currentInput, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Length)
            .ToList();
    }

    private IEnumerable<string> GetSubcommandSuggestions(string command)
    {
        return command switch
        {
            "/help" => ["commands", "mcp", "speckit"],
            "/spaces" => ["list", "show", "remove", "add"],
            "/config" => ["list", "get", "set"],
            "/hub" => ["search", "show", "install"],
            "/mcp" => ["list", "search", "install", "show"],
            "/history" => ["last", "all"],
            _ => Enumerable.Empty<string>()
        };
    }

    private List<string> GetFileSuggestions(string textBeforeAt, string partialPath)
    {
        if (string.IsNullOrWhiteSpace(CurrentDirectory))
        {
            // In global mode, we don't allow relative file selection to avoid leaking project files.
            return new List<string>();
        }

        var baseDir = CurrentDirectory;
        if (!Directory.Exists(baseDir)) return new List<string>();

        try
        {
            // Normalize partial path (replace forward slashes with system separators)
            var normalizedPartial = partialPath.Replace('/', Path.DirectorySeparatorChar);
            
            var searchDir = baseDir;
            var searchPattern = "*";
            
            if (normalizedPartial.Contains(Path.DirectorySeparatorChar))
            {
                var lastSeparatorPos = normalizedPartial.LastIndexOf(Path.DirectorySeparatorChar);
                var subPath = normalizedPartial[..lastSeparatorPos];
                searchDir = Path.Combine(baseDir, subPath);
                searchPattern = normalizedPartial[(lastSeparatorPos + 1)..] + "*";
                
                if (!Directory.Exists(searchDir)) return new List<string>();
            }
            else
            {
                searchPattern = normalizedPartial + "*";
            }

            var entries = Directory.GetFileSystemEntries(searchDir, searchPattern)
                .Select(e => Path.GetRelativePath(baseDir, e))
                .Select(e => e.Replace(Path.DirectorySeparatorChar, '/')) // Always use forward slashes for cross-platform
                .Select(e => textBeforeAt + "@" + e)
                .OrderBy(e => e.Length)
                .Take(20)
                .ToList();

            return entries;
        }
        catch
        {
            return new List<string>();
        }
    }


    private static int GetDisplayWidth(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        int width = 0;
        foreach (char c in s)
        {
            // Simple logic for CJK characters: 
            // if character code > 255, it's likely double-width in many terminals.
            // More precise logic would involve Unicode categories or specific ranges.
            width += (c > 255) ? 2 : 1;
        }
        return width;
    }

    private void RenderMenu()
    {
        var displayCount = Math.Min(MaxVisibleMenuLines, _currentMatches.Count);
        _lastMenuLineCount = displayCount;

        for (int i = 0; i < displayCount; i++)
        {
            AnsiConsole.Write("\n");
            var matchIndex = _menuStartIndex + i;
            if (matchIndex >= _currentMatches.Count) break;

            var match = _currentMatches[matchIndex];
            
            // Render indicator for scrollable above/below
            var prefix = "  ";
            if (i == 0 && _menuStartIndex > 0) prefix = "↑ ";
            else if (i == displayCount - 1 && _menuStartIndex + displayCount < _currentMatches.Count) prefix = "↓ ";

            if (matchIndex == _menuSelectionIndex)
            {
                AnsiConsole.Markup($"[blue]{prefix}>[/] [white on blue]{match.EscapeMarkup()}[/]");
            }
            else
            {
                AnsiConsole.Markup($"  [grey]{match.EscapeMarkup()}[/]");
            }
            
            // Clear rest of menu line
            var currentPos = Console.CursorLeft;
            AnsiConsole.Write(new string(' ', Math.Max(0, Console.WindowWidth - currentPos - 1)));
        }

        AnsiConsole.Cursor.MoveUp(displayCount);
    }

    private void ClearMenuArea()
    {
        if (_lastMenuLineCount == 0) return;

        for (int i = 0; i < _lastMenuLineCount; i++)
        {
            AnsiConsole.Write("\n");
            AnsiConsole.Write(new string(' ', Console.WindowWidth - 1));
        }

        AnsiConsole.Cursor.MoveUp(_lastMenuLineCount);
        _lastMenuLineCount = 0;
    }
}
