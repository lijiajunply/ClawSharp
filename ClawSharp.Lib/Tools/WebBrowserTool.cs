using System.Text.Json;
using Microsoft.Playwright;
using ClawSharp.Lib.Core;

namespace ClawSharp.Lib.Tools;

/// <summary>
/// A tool that uses Playwright to fetch web content, supporting JavaScript rendering.
/// </summary>
public sealed class WebBrowserTool : IToolExecutor
{
    /// <inheritdoc />
    public ToolDefinition Definition { get; } = new(
        "web_browser",
        "Fetch content from a web URL, supporting JavaScript-heavy sites.",
        ToolSecurity.Json(new
        {
            type = "object",
            properties = new
            {
                url = new { type = "string", description = "The target URL to fetch." },
                wait_selector = new { type = "string", description = "Optional: CSS selector to wait for before fetching." },
                wait_time = new { type = "integer", description = "Optional: Hard wait time in milliseconds." }
            },
            required = new[] { "url" }
        }),
        null,
        ToolCapability.NetworkAccess);

    /// <inheritdoc />
    public async Task<ToolInvocationResult> ExecuteAsync(ToolExecutionContext context, JsonElement arguments)
    {
        var url = arguments.GetProperty("url").GetString() ?? string.Empty;
        var waitSelector = arguments.TryGetProperty("wait_selector", out var ws) ? ws.GetString() : null;
        var waitTime = arguments.TryGetProperty("wait_time", out var wt) ? wt.GetInt32() : 0;

        try
        {
            using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
            
            IBrowser browser;
            try
            {
                browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            }
            catch (Microsoft.Playwright.PlaywrightException ex) when (ex.Message.Contains("Executable doesn't exist"))
            {
                throw new EnvironmentDependencyException(Definition.Name, 
                    "Playwright browsers are not installed. This is required for the 'web_browser' tool.",
                    "dotnet run --project ClawSharp.Lib -- playwright install chromium");
            }
            
            var page = await browser.NewPageAsync();

            var timeoutSeconds = context.Permissions.TimeoutSeconds ?? 60;
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = timeoutSeconds * 1000 });

            if (!string.IsNullOrEmpty(waitSelector))
            {
                await page.WaitForSelectorAsync(waitSelector, new PageWaitForSelectorOptions { Timeout = 30000 });
            }

            if (waitTime > 0)
            {
                await Task.Delay(waitTime, timeoutCts.Token);
            }

            // Extract content and clean HTML (basic text extraction)
            var content = await page.EvaluateAsync<string>("() => document.body.innerText");
            
            await browser.CloseAsync();

            // Truncate based on MaxOutputLength
            var maxLength = context.Permissions.MaxOutputLength ?? 16_384;
            if (content.Length > maxLength)
            {
                content = content[..maxLength];
            }

            return ToolInvocationResult.Success(Definition.Name, ToolSecurity.Json(new { url, content }));
        }
        catch (Exception ex)
        {
            return ToolInvocationResult.Failure(Definition.Name, $"Failed to fetch web content: {ex.Message}");
        }
    }
}
