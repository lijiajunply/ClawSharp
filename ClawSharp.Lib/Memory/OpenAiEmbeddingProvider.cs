using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ClawSharp.Lib.Configuration;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ClawSharp.Lib.Memory;

/// <summary>
/// 使用 OpenAI API 生成向量。
/// </summary>
public sealed class OpenAiEmbeddingProvider : IEmbeddingProvider
{
    private readonly ClawOptions _options;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// 使用应用配置和 HTTP 客户端工厂创建 OpenAI 向量提供器。
    /// </summary>
    /// <param name="options">应用配置。</param>
    /// <param name="httpClientFactory">HTTP 客户端工厂。</param>
    public OpenAiEmbeddingProvider(ClawOptions options, IHttpClientFactory httpClientFactory)
    {
        _options = options;
        _httpClient = httpClientFactory.CreateClient();
    }

    /// <summary>
    /// 当前配置的输出向量维度。
    /// </summary>
    public int Dimensions => _options.Embedding.Dimensions;
    
    /// <summary>
    /// 调用 OpenAI Embeddings API 为输入文本批量生成向量。
    /// </summary>
    /// <param name="texts">待嵌入的文本集合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>按输入顺序排列的向量列表。</returns>
    public async Task<IReadOnlyList<EmbeddingVector>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        if (texts.Count == 0) return Array.Empty<EmbeddingVector>();

        var apiKey = _options.Embedding.ApiKey ?? _options.Providers.Models.FirstOrDefault(m => m.Type == "openai-responses")?.ApiKey;
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("OpenAI API Key is not configured for Embedding.");
        }

        var baseUrl = _options.Embedding.BaseUrl ?? "https://api.openai.com/v1/";
        if (!baseUrl.EndsWith("/")) baseUrl += "/";

        var request = new OpenAiEmbeddingRequest
        {
            Input = texts,
            Model = _options.Embedding.Model
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}embeddings");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpRequest.Content = JsonContent.Create(request);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OpenAiEmbeddingResponse>(cancellationToken: cancellationToken);
        if (result?.Data == null) return Array.Empty<EmbeddingVector>();

        return result.Data.OrderBy(d => d.Index).Select(d => new EmbeddingVector(d.Index.ToString(), d.Embedding)).ToList();
    }

    private sealed class OpenAiEmbeddingRequest
    {
        [JsonPropertyName("input")]
        public IReadOnlyList<string> Input { get; set; } = null!;

        [JsonPropertyName("model")]
        public string Model { get; set; } = null!;
    }

    private sealed class OpenAiEmbeddingResponse
    {
        [JsonPropertyName("data")]
        public List<OpenAiEmbeddingData>? Data { get; set; }
    }

    private sealed class OpenAiEmbeddingData
    {
        [JsonPropertyName("embedding")]
        public float[] Embedding { get; set; } = null!;

        [JsonPropertyName("index")]
        public int Index { get; set; }
    }
}
