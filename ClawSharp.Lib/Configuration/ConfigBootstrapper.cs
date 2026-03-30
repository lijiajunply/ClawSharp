using System.Text.Json;

namespace ClawSharp.Lib.Configuration;

/// <summary>
/// <see cref="IConfigBootstrapper"/> 的默认实现。
/// </summary>
public sealed class ConfigBootstrapper : IConfigBootstrapper
{
    /// <inheritdoc/>
    public IEnumerable<ProviderTemplate> GetProviderTemplates()
    {
        return new[]
        {
            new ProviderTemplate { Name = "OpenAI", Id = "openai", Type = "openai-responses" },
            new ProviderTemplate { Name = "Anthropic", Id = "anthropic", Type = "anthropic-messages" },
            new ProviderTemplate { Name = "Gemini (OpenAI-compatible)", Id = "gemini", Type = "openai-chat-compatible" },
            new ProviderTemplate { Name = "DeepSeek (OpenAI-compatible)", Id = "deepseek", Type = "openai-chat-compatible" },
            new ProviderTemplate { Name = "Stub (for testing)", Id = "stub", Type = "stub" }
        };
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
                        ApiKey = input.ApiKey
                    }
                }
            }
        };

        return JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <inheritdoc/>
    public async Task SaveConfigAsync(string path, string json)
    {
        await File.WriteAllTextAsync(path, json);
    }
}
