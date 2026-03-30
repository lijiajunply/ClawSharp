using System.Net.Http.Json;
using System.Text.Json;
using ClawSharp.Lib.Configuration;
using Microsoft.Playwright;

namespace ClawSharp.Lib.Tools;

/// <summary>
/// A tool that performs web searches using various providers.
/// </summary>
public sealed class WebSearchTool(ClawOptions options) : IToolExecutor
{
    private readonly SearchOptions _searchOptions = options.Tools.Search;

    /// <inheritdoc />
    public ToolDefinition Definition { get; } = new(
        "web_search",
        "Search the web for information using a search engine.",
        ToolSecurity.Json(new
        {
            type = "object",
            properties = new
            {
                query = new { type = "string", description = "The search query." },
                limit = new { type = "integer", description = "Optional: Maximum number of search results to return." }
            },
            required = new[] { "query" }
        }),
        null,
        ToolCapability.NetworkAccess);

    /// <inheritdoc />
    public async Task<ToolInvocationResult> ExecuteAsync(ToolExecutionContext context, JsonElement arguments)
    {
        var query = arguments.GetProperty("query").GetString() ?? string.Empty;
        var limit = arguments.TryGetProperty("limit", out var l) ? l.GetInt32() : _searchOptions.DefaultLimit;

        try
        {
            // Implementation logic based on provider
            return _searchOptions.Provider.ToLowerInvariant() switch
            {
                "duckduckgo" => await SearchDuckDuckGoAsync(query, limit, context.CancellationToken),
                "tavily" => await SearchTavilyAsync(query, limit, context.CancellationToken),
                _ => await SearchDuckDuckGoAsync(query, limit, context.CancellationToken) // Fallback
            };
        }
        catch (Exception ex)
        {
            return ToolInvocationResult.Failure(Definition.Name, $"Web search failed: {ex.Message}");
        }
    }

    private async Task<ToolInvocationResult> SearchDuckDuckGoAsync(string query, int limit, CancellationToken ct)
    {
        // For DuckDuckGo, we use Playwright to navigate and scrape results safely without an API key
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();

        try
        {
            var searchUrl = $"https://duckduckgo.com/html/?q={Uri.EscapeDataString(query)}";
            await page.GotoAsync(searchUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

            // Basic scraping of DuckDuckGo HTML results
            var results = await page.EvaluateAsync<List<SearchResult>>(@"
                () => {
                    const items = Array.from(document.querySelectorAll('.result'));
                    return items.map(item => {
                        const titleEl = item.querySelector('.result__title a');
                        const snippetEl = item.querySelector('.result__snippet');
                        return {
                            title: titleEl ? titleEl.innerText : '',
                            url: titleEl ? titleEl.href : '',
                            snippet: snippetEl ? snippetEl.innerText : ''
                        };
                    }).filter(r => r.title && r.url);
                }
            ");

            await browser.CloseAsync();

            var finalResults = results.Take(limit).ToList();
            return ToolInvocationResult.Success(Definition.Name, ToolSecurity.Json(new { query, results = finalResults }));
        }
        catch (Exception)
        {
            await browser.CloseAsync();
            throw;
        }
    }

    private async Task<ToolInvocationResult> SearchTavilyAsync(string query, int limit, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_searchOptions.ApiKey))
        {
            return ToolInvocationResult.Failure(Definition.Name, "Tavily API key is missing in configuration.");
        }

        using var client = new HttpClient();
        var requestBody = new
        {
            api_key = _searchOptions.ApiKey,
            query,
            max_results = limit
        };

        var response = await client.PostAsJsonAsync("https://api.tavily.com/search", requestBody, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return ToolInvocationResult.Failure(Definition.Name, $"Tavily search failed: {error}");
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        return ToolInvocationResult.Success(Definition.Name, json);
    }

    private record SearchResult(string title, string url, string snippet);
}
