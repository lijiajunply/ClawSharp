using System.Net;
using System.Text;
using ClawSharp.Lib.Agents;
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Providers;
using ClawSharp.Lib.Tools;

namespace ClawSharp.Lib.Tests;

public sealed class ProviderTests
{
    [Fact]
    public async Task StubProvider_CompleteMatchesStreamingAggregation()
    {
        var provider = new StubModelProvider();
        var request = new ModelRequest(
            new ResolvedModelTarget("stub", "stub", "http://localhost/stub", "stub-model"),
            "session-1",
            "trace-1",
            [new ModelMessage(ModelMessageRole.User, "hello world")],
            []);

        var streamed = new List<string>();
        await foreach (var chunk in provider.StreamAsync(request))
        {
            if (!string.IsNullOrEmpty(chunk.TextDelta))
            {
                streamed.Add(chunk.TextDelta);
            }
        }

        var completed = await ((IModelProvider)provider).CompleteAsync(request);
        Assert.Equal(string.Concat(streamed), completed.Content);
        Assert.Equal(ModelStopReason.Completed, completed.StopReason);
    }

    [Fact]
    public void ProviderResolver_UsesDefaultProviderAndAgentModelOverride()
    {
        var options = new ClawOptions
        {
            Providers = new ProviderOptions
            {
                DefaultProvider = "openai",
                Models =
                [
                    new ModelProviderOptions
                    {
                        Name = "openai",
                        Type = "openai-responses",
                        BaseUrl = "https://api.openai.com",
                        ApiKey = "key",
                        DefaultModel = "gpt-default",
                        SupportsResponses = true
                    }
                ]
            }
        };

        var resolver = new ModelProviderResolver(options, new ModelProviderRegistry([new StubModelProvider(), new OpenAiResponsesModelProvider(options, new FakeHttpClientFactory())]));
        var agent = new AgentDefinition("planner", "Planner", "desc", "", "gpt-agent", "prompt", [], [], "workspace", [], ToolPermissionSet.Empty, "v1", "");

        var resolved = resolver.Resolve(agent);
        Assert.Equal("openai", resolved.Target.ProviderName);
        Assert.Equal("openai-responses", resolved.Target.ProviderType);
        Assert.Equal("gpt-agent", resolved.Target.Model);
    }

    [Fact]
    public async Task OpenAiResponsesProvider_BuildsRequestAndParsesStream()
    {
        var handler = new FakeHttpMessageHandler(_ =>
        {
            var stream = """
data: {"type":"response.output_text.delta","delta":"Hello "}

data: {"type":"response.output_text.delta","delta":"world"}

data: {"type":"response.completed","response":{"usage":{"input_tokens":10,"output_tokens":2,"total_tokens":12}}}

""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(stream, Encoding.UTF8, "text/event-stream")
            };
        });
        var options = new ClawOptions
        {
            Providers = new ProviderOptions
            {
                Models =
                [
                    new ModelProviderOptions
                    {
                        Name = "openai",
                        Type = "openai-responses",
                        BaseUrl = "https://api.openai.com",
                        ApiKey = "key",
                        DefaultModel = "gpt-test",
                        SupportsResponses = true
                    }
                ]
            }
        };
        var provider = new OpenAiResponsesModelProvider(options, new FakeHttpClientFactory(handler));
        var request = new ModelRequest(
            new ResolvedModelTarget("openai", "openai-responses", "https://api.openai.com", "gpt-test", SupportsResponses: true),
            "session-1",
            "trace-1",
            [new ModelMessage(ModelMessageRole.User, "hello")],
            [new ModelToolSchema("system.info", "Get info", """{"type":"object","properties":{}}""")],
            "Be helpful");

        var chunks = await CollectChunks(provider.StreamAsync(request));
        var sentBody = await handler.Requests.Single().Content!.ReadAsStringAsync();

        Assert.Contains("/v1/responses", handler.Requests.Single().RequestUri!.ToString());
        Assert.Contains("\"tools\"", sentBody);
        Assert.Equal("Bearer", handler.Requests.Single().Headers.Authorization?.Scheme);
        Assert.Contains(chunks, chunk => chunk.TextDelta == "Hello ");
        Assert.Contains(chunks, chunk => chunk.StopReason == ModelStopReason.Completed);
    }

    [Fact]
    public async Task OpenAiResponsesProvider_ParsesFunctionCall()
    {
        var handler = new FakeHttpMessageHandler(_ =>
        {
            var stream = """
data: {"type":"response.function_call_arguments.delta","item_id":"call_1","name":"system.info","delta":"{"}

data: {"type":"response.function_call_arguments.delta","item_id":"call_1","delta":"}"}

data: {"type":"response.output_item.done","item":{"type":"function_call","call_id":"call_1","name":"system.info","arguments":"{}"}}

data: {"type":"response.completed","response":{"usage":{"input_tokens":1,"output_tokens":1,"total_tokens":2}}}

""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(stream, Encoding.UTF8, "text/event-stream")
            };
        });

        var options = BuildRemoteOptions("openai", "openai-responses");
        var provider = new OpenAiResponsesModelProvider(options, new FakeHttpClientFactory(handler));
        var request = new ModelRequest(
            new ResolvedModelTarget("openai", "openai-responses", "https://api.openai.com", "gpt-test", SupportsResponses: true),
            "session-1",
            "trace-1",
            [new ModelMessage(ModelMessageRole.User, "run tool")],
            [new ModelToolSchema("system.info", "Get info")]);

        var chunks = await CollectChunks(provider.StreamAsync(request));
        var toolCall = chunks.Single(chunk => chunk.ToolCall is not null).ToolCall!;

        Assert.Equal("system.info", toolCall.Name);
        Assert.Equal("{}", toolCall.ArgumentsJson);
    }

    [Fact]
    public async Task OpenAiCompatibleProvider_ParsesToolCallsAndText()
    {
        var handler = new FakeHttpMessageHandler(_ =>
        {
            var stream = """
data: {"choices":[{"delta":{"content":"Hi"}}]}

data: {"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call_1","function":{"name":"system.info","arguments":"{"}}]}}]}

data: {"choices":[{"delta":{"tool_calls":[{"index":0,"function":{"arguments":"}"}}]},"finish_reason":"tool_calls"}],"usage":{"prompt_tokens":2,"completion_tokens":1,"total_tokens":3}}

""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(stream, Encoding.UTF8, "text/event-stream")
            };
        });
        var options = BuildRemoteOptions("compat", "openai-chat-compatible");
        var provider = new OpenAiCompatibleChatModelProvider(options, new FakeHttpClientFactory(handler));
        var request = new ModelRequest(
            new ResolvedModelTarget("compat", "openai-chat-compatible", "https://compat.example", "gpt-compat", RequestPath: "v1/chat/completions", SupportsChatCompletions: true),
            "session-1",
            "trace-1",
            [new ModelMessage(ModelMessageRole.User, "hello")],
            [new ModelToolSchema("system.info", "Get info")]);

        var chunks = await CollectChunks(provider.StreamAsync(request));

        Assert.Contains(chunks, chunk => chunk.TextDelta == "Hi");
        Assert.Contains(chunks, chunk => chunk.ToolCall?.Name == "system.info");
        Assert.Contains(chunks, chunk => chunk.StopReason == ModelStopReason.ToolCall);
    }

    [Fact]
    public async Task OpenAiProvider_MissingApiKey_ThrowsClearError()
    {
        var options = new ClawOptions
        {
            Providers = new ProviderOptions
            {
                Models =
                [
                    new ModelProviderOptions
                    {
                        Name = "openai",
                        Type = "openai-responses",
                        BaseUrl = "https://api.openai.com",
                        DefaultModel = "gpt-test",
                        SupportsResponses = true
                    }
                ]
            }
        };
        var provider = new OpenAiResponsesModelProvider(options, new FakeHttpClientFactory());
        var request = new ModelRequest(
            new ResolvedModelTarget("openai", "openai-responses", "https://api.openai.com", "gpt-test", SupportsResponses: true),
            "session-1",
            "trace-1",
            [new ModelMessage(ModelMessageRole.User, "hello")],
            []);

        var ex = await Assert.ThrowsAsync<ModelProviderException>(async () =>
        {
            await foreach (var _ in provider.StreamAsync(request))
            {
            }
        });

        Assert.Contains("ApiKey", ex.Message);
    }

    [Fact]
    public void ProviderResolver_UsesAgentProviderOverrideAndProviderDefaultModel()
    {
        var options = new ClawOptions
        {
            Providers = new ProviderOptions
            {
                DefaultProvider = "openai",
                Models =
                [
                    new ModelProviderOptions
                    {
                        Name = "openai",
                        Type = "openai-responses",
                        BaseUrl = "https://api.openai.com",
                        ApiKey = "key",
                        DefaultModel = "gpt-default",
                        SupportsResponses = true
                    },
                    new ModelProviderOptions
                    {
                        Name = "claude",
                        Type = "anthropic-messages",
                        BaseUrl = "https://api.anthropic.com",
                        ApiKey = "anthropic-key",
                        DefaultModel = "claude-sonnet",
                        SupportsResponses = false
                    }
                ]
            }
        };

        var resolver = new ModelProviderResolver(options, new ModelProviderRegistry(
        [
            new StubModelProvider(),
            new OpenAiResponsesModelProvider(options, new FakeHttpClientFactory()),
            new AnthropicMessagesModelProvider(options, new FakeHttpClientFactory())
        ]));

        var agent = new AgentDefinition("planner", "Planner", "desc", "claude", "", "prompt", [], [], "workspace", [], ToolPermissionSet.Empty, "v1", "");

        var resolved = resolver.Resolve(agent);

        Assert.Equal("claude", resolved.Target.ProviderName);
        Assert.Equal("anthropic-messages", resolved.Target.ProviderType);
        Assert.Equal("claude-sonnet", resolved.Target.Model);
    }

    [Fact]
    public async Task AnthropicProvider_BuildsRequestAndParsesStream()
    {
        var handler = new FakeHttpMessageHandler(_ =>
        {
            var stream = """
data: {"type":"content_block_delta","delta":{"type":"text_delta","text":"Hello "}}

data: {"type":"content_block_delta","delta":{"type":"text_delta","text":"Claude"}}

data: {"type":"message_delta","delta":{"stop_reason":"end_turn"},"usage":{"input_tokens":9,"output_tokens":2}}

data: {"type":"message_stop"}

""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(stream, Encoding.UTF8, "text/event-stream")
            };
        });

        var options = new ClawOptions
        {
            Providers = new ProviderOptions
            {
                Models =
                [
                    new ModelProviderOptions
                    {
                        Name = "claude",
                        Type = "anthropic-messages",
                        BaseUrl = "https://api.anthropic.com",
                        ApiKey = "anthropic-key",
                        DefaultModel = "claude-sonnet"
                    }
                ]
            }
        };

        var provider = new AnthropicMessagesModelProvider(options, new FakeHttpClientFactory(handler));
        var request = new ModelRequest(
            new ResolvedModelTarget("claude", "anthropic-messages", "https://api.anthropic.com", "claude-sonnet"),
            "session-1",
            "trace-1",
            [new ModelMessage(ModelMessageRole.User, "hello")],
            [],
            "Be helpful");

        var chunks = await CollectChunks(provider.StreamAsync(request));
        var sentBody = await handler.Requests.Single().Content!.ReadAsStringAsync();
        var sentRequest = handler.Requests.Single();

        Assert.Contains("/v1/messages", sentRequest.RequestUri!.ToString());
        Assert.Equal("anthropic-key", sentRequest.Headers.GetValues("x-api-key").Single());
        Assert.Equal("2023-06-01", sentRequest.Headers.GetValues("anthropic-version").Single());
        Assert.Contains("\"system\":\"Be helpful\"", sentBody);
        Assert.Contains(chunks, chunk => chunk.TextDelta == "Hello ");
        Assert.Contains(chunks, chunk => chunk.StopReason == ModelStopReason.Completed);
    }

    [Fact]
    public async Task AnthropicProvider_ParsesNativeToolUse()
    {
        var handler = new FakeHttpMessageHandler(_ =>
        {
            var stream = """
data: {"type":"content_block_start","index":0,"content_block":{"type":"tool_use","id":"toolu_1","name":"system.info","input":{}}}

data: {"type":"content_block_delta","index":0,"delta":{"type":"input_json_delta","partial_json":"{\"verbose\":true"}}

data: {"type":"content_block_delta","index":0,"delta":{"type":"input_json_delta","partial_json":"}"}}

data: {"type":"content_block_stop","index":0}

data: {"type":"message_delta","delta":{"stop_reason":"tool_use"},"usage":{"input_tokens":7,"output_tokens":3}}

data: {"type":"message_stop"}

""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(stream, Encoding.UTF8, "text/event-stream")
            };
        });

        var options = new ClawOptions
        {
            Providers = new ProviderOptions
            {
                Models =
                [
                    new ModelProviderOptions
                    {
                        Name = "claude",
                        Type = "anthropic-messages",
                        BaseUrl = "https://api.anthropic.com",
                        ApiKey = "anthropic-key",
                        DefaultModel = "claude-sonnet"
                    }
                ]
            }
        };

        var provider = new AnthropicMessagesModelProvider(options, new FakeHttpClientFactory(handler));
        var request = new ModelRequest(
            new ResolvedModelTarget("claude", "anthropic-messages", "https://api.anthropic.com", "claude-sonnet"),
            "session-1",
            "trace-1",
            [new ModelMessage(ModelMessageRole.User, "run tool")],
            [new ModelToolSchema("system.info", "Get info", """{"type":"object","properties":{"verbose":{"type":"boolean"}}}""")]);

        var chunks = await CollectChunks(provider.StreamAsync(request));
        var toolCall = chunks.Single(chunk => chunk.ToolCall is not null).ToolCall!;
        var sentBody = await handler.Requests.Single().Content!.ReadAsStringAsync();

        Assert.Equal("system.info", toolCall.Name);
        Assert.Equal("toolu_1", toolCall.Id);
        Assert.Equal("{\"verbose\":true}", toolCall.ArgumentsJson);
        Assert.Contains("\"tools\"", sentBody);
        Assert.Contains("\"input_schema\"", sentBody);
        Assert.Contains(chunks, chunk => chunk.StopReason == ModelStopReason.ToolCall);
    }

    [Fact]
    public async Task GeminiCompatibleProvider_UsesGeminiDefaultPath()
    {
        var handler = new FakeHttpMessageHandler(_ =>
        {
            var stream = """
data: {"choices":[{"delta":{"content":"Hi Gemini"},"finish_reason":"stop"}],"usage":{"prompt_tokens":2,"completion_tokens":2,"total_tokens":4}}

""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(stream, Encoding.UTF8, "text/event-stream")
            };
        });

        var options = BuildRemoteOptions("gemini", "gemini-openai-compatible", "https://generativelanguage.googleapis.com/v1beta/openai");
        var provider = new GeminiCompatibleChatModelProvider(options, new FakeHttpClientFactory(handler));
        var request = new ModelRequest(
            new ResolvedModelTarget("gemini", "gemini-openai-compatible", "https://generativelanguage.googleapis.com/v1beta/openai", "gemini-2.5-flash", SupportsChatCompletions: true),
            "session-1",
            "trace-1",
            [new ModelMessage(ModelMessageRole.User, "hello")],
            []);

        var chunks = await CollectChunks(provider.StreamAsync(request));

        Assert.Contains("/chat/completions", handler.Requests.Single().RequestUri!.ToString());
        Assert.DoesNotContain("/v1/chat/completions", handler.Requests.Single().RequestUri!.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(chunks, chunk => chunk.TextDelta == "Hi Gemini");
        Assert.Contains(chunks, chunk => chunk.StopReason == ModelStopReason.Completed);
    }

    private static async Task<List<ModelResponseChunk>> CollectChunks(IAsyncEnumerable<ModelResponseChunk> stream)
    {
        var chunks = new List<ModelResponseChunk>();
        await foreach (var chunk in stream)
        {
            chunks.Add(chunk);
        }

        return chunks;
    }

    private static ClawOptions BuildRemoteOptions(string name, string type, string? baseUrl = null) => new()
    {
        Providers = new ProviderOptions
        {
            Models =
            [
                new ModelProviderOptions
                {
                    Name = name,
                    Type = type,
                    BaseUrl = baseUrl ?? (type == "openai-chat-compatible" ? "https://compat.example" : "https://api.openai.com"),
                    ApiKey = "key",
                    DefaultModel = type == "gemini-openai-compatible" ? "gemini-2.5-flash" : "gpt-test",
                    SupportsResponses = type == "openai-responses",
                    SupportsChatCompletions = type is "openai-chat-compatible" or "gemini-openai-compatible"
                }
            ]
        }
    };

    private sealed class FakeHttpClientFactory(HttpMessageHandler? handler = null) : IProviderHttpClientFactory
    {
        public HttpClient CreateClient(ResolvedModelTarget target)
        {
            return handler is null
                ? new HttpClient { BaseAddress = new Uri(target.BaseUrl.EndsWith('/') ? target.BaseUrl : $"{target.BaseUrl}/", UriKind.Absolute) }
                : new HttpClient(handler) { BaseAddress = new Uri(target.BaseUrl.EndsWith('/') ? target.BaseUrl : $"{target.BaseUrl}/", UriKind.Absolute) };
        }
    }

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(CloneRequest(request));
            return Task.FromResult(responder(request));
        }

        private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri);
            foreach (var header in request.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            if (request.Content is not null)
            {
                var content = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                clone.Content = new StringContent(content, Encoding.UTF8, request.Content.Headers.ContentType?.MediaType);
            }

            return clone;
        }
    }
}
