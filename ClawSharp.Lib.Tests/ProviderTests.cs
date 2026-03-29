using ClawSharp.Lib.Providers;

namespace ClawSharp.Lib.Tests;

public sealed class ProviderTests
{
    [Fact]
    public async Task StubProvider_CompleteMatchesStreamingAggregation()
    {
        var provider = new StubModelProvider();
        var request = new ModelRequest(
            "stub",
            "stub-model",
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
}
