using System.Text.Json;
using ClawSharp.Lib.Mcp;
using Xunit;

namespace ClawSharp.Lib.Tests;

public class McpTests
{
    [Fact]
    public void RequestSerialization_ShouldMatchJsonRpc()
    {
        var request = new McpRequest
        {
            Id = JsonDocument.Parse("1").RootElement,
            Method = "test",
            Params = new { foo = "bar" }
        };

        var json = JsonSerializer.Serialize(request);
        using var doc = JsonDocument.Parse(json);

        Assert.Equal("2.0", doc.RootElement.GetProperty("jsonrpc").GetString());
        Assert.Equal("test", doc.RootElement.GetProperty("method").GetString());
        Assert.Equal("bar", doc.RootElement.GetProperty("params").GetProperty("foo").GetString());
    }

    [Fact]
    public void ResponseDeserialization_ShouldHandleResult()
    {
        var json = "{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"status\":\"ok\"}}";
        var response = JsonSerializer.Deserialize<McpResponse>(json);

        Assert.NotNull(response);
        Assert.Equal("2.0", response.Jsonrpc);
        Assert.Equal("1", response.Id?.ToString());
        Assert.NotNull(response.Result);
        
        var result = (JsonElement)response.Result;
        Assert.Equal("ok", result.GetProperty("status").GetString());
    }

    [Fact]
    public void ResponseDeserialization_ShouldHandleError()
    {
        var json = "{\"jsonrpc\":\"2.0\",\"id\":1,\"error\":{\"code\":-32601,\"message\":\"Method not found\"}}";
        var response = JsonSerializer.Deserialize<McpResponse>(json);

        Assert.NotNull(response);
        Assert.NotNull(response.Error);
        Assert.Equal(-32601, response.Error.Code);
        Assert.Equal("Method not found", response.Error.Message);
    }
}
