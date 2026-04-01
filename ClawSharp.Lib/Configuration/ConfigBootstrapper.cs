using System.Text.Json;
using System.Text.Json.Serialization;
using ClawSharp.Lib.Runtime;

namespace ClawSharp.Lib.Configuration;

/// <summary>
/// <see cref="IConfigBootstrapper"/> 的默认实现。
/// </summary>
public sealed class ConfigBootstrapper : IConfigBootstrapper
{
    /// <inheritdoc/>
    public IEnumerable<ProviderTemplate> GetProviderTemplates(EnvironmentDiscoveryResult? discovery = null)
    {
        var templates = new List<ProviderTemplate>();

        if (discovery?.Ollama.Available == true)
        {
            templates.Add(new ProviderTemplate
            {
                Name = "Ollama (Auto-discovered)",
                Id = "ollama-local",
                Type = "openai-chat-compatible",
                BaseUrl = discovery.Ollama.BaseUrl ?? "http://127.0.0.1:11434",
                DefaultModel = discovery.Ollama.Models.FirstOrDefault() ?? string.Empty,
                RequiresApiKey = false,
                SupportsChatCompletions = true
            });
        }

        if (discovery?.LlamaEdge.Available == true)
        {
            templates.Add(new ProviderTemplate
            {
                Name = "LlamaEdge (Auto-discovered)",
                Id = "llamaedge-local",
                Type = "openai-chat-compatible",
                BaseUrl = discovery.LlamaEdge.BaseUrl ?? "http://127.0.0.1:8080",
                DefaultModel = discovery.LlamaEdge.Models.FirstOrDefault() ?? string.Empty,
                RequiresApiKey = false,
                SupportsChatCompletions = true
            });
        }

        templates.AddRange(
        [
            new ProviderTemplate
            {
                Name = "OpenAI",
                Id = "openai",
                Type = "openai-responses",
                BaseUrl = "https://api.openai.com",
                RequiresApiKey = true,
                SupportsResponses = true
            },
            new ProviderTemplate
            {
                Name = "Anthropic",
                Id = "anthropic",
                Type = "anthropic-messages",
                BaseUrl = "https://api.anthropic.com",
                RequiresApiKey = true
            },
            new ProviderTemplate
            {
                Name = "Gemini (OpenAI-compatible)",
                Id = "gemini",
                Type = "openai-chat-compatible",
                BaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai",
                RequiresApiKey = true,
                SupportsChatCompletions = true
            },
            new ProviderTemplate
            {
                Name = "DeepSeek (OpenAI-compatible)",
                Id = "deepseek",
                Type = "openai-chat-compatible",
                BaseUrl = "https://api.deepseek.com",
                RequiresApiKey = true,
                SupportsChatCompletions = true
            },
            new ProviderTemplate
            {
                Name = "Stub (for testing)",
                Id = "stub",
                Type = "stub",
                BaseUrl = "http://localhost/stub",
                RequiresApiKey = false
            }
        ]);

        return templates;
    }

    /// <inheritdoc/>
    public string GenerateConfigJson(BootstrapConfig input)
    {
        var config = new
        {
            Runtime = new
            {
                WorkspaceRoot = input.WorkspaceRoot,
                DataPath = input.DataPath
            },
            Providers = new
            {
                DefaultProvider = input.DefaultProvider,
                Models = new[]
                {
                    new
                    {
                        Name = input.DefaultProvider,
                        Type = input.ProviderType,
                        BaseUrl = input.BaseUrl,
                        ApiKey = input.ApiKey,
                        DefaultModel = input.DefaultModel,
                        RequestPath = input.RequestPath,
                        SupportsResponses = input.SupportsResponses,
                        SupportsChatCompletions = input.SupportsChatCompletions
                    }
                }
            }
        };

        return JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
    }

    /// <inheritdoc/>
    public async Task SaveConfigAsync(string path, string json)
    {
        await File.WriteAllTextAsync(path, json);
    }
}
