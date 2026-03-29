using ClawSharp.Lib.Configuration;
using Microsoft.Extensions.Configuration;

namespace ClawSharp.Lib.Tests;

public sealed class ConfigurationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "claw-config-tests", Guid.NewGuid().ToString("N"));

    public ConfigurationTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void Configuration_FollowsExpectedPrecedence()
    {
        File.WriteAllText(Path.Combine(_root, "appsettings.json"), """
{
  "Runtime": {
    "WorkspaceRoot": "json-root"
  }
}
""");
        File.WriteAllText(Path.Combine(_root, "appsettings.Local.json"), """
{
  "Runtime": {
    "WorkspaceRoot": "local-root"
  }
}
""");
        File.WriteAllText(Path.Combine(_root, ".env"), "Runtime__WorkspaceRoot=env-root");
        Environment.SetEnvironmentVariable("Runtime__WorkspaceRoot", "process-root");

        var configuration = ClawConfigurationLoader.Build(_root, new Dictionary<string, string?> { ["Runtime:WorkspaceRoot"] = "override-root" });
        var options = configuration.Get<ClawOptions>()!;

        Assert.Equal("override-root", options.Runtime.WorkspaceRoot);

        Environment.SetEnvironmentVariable("Runtime__WorkspaceRoot", null);
    }

    [Fact]
    public void Configuration_BindsProviderSettingsFromEnvAndJson()
    {
        File.WriteAllText(Path.Combine(_root, "appsettings.json"), """
{
  "Providers": {
    "DefaultProvider": "openai",
    "Models": [
      {
        "Name": "openai",
        "Type": "openai-responses",
        "BaseUrl": "https://api.openai.com",
        "DefaultModel": "gpt-json"
      }
    ]
  }
}
""");
        Environment.SetEnvironmentVariable("Providers__Models__0__ApiKey", "env-key");

        var configuration = ClawConfigurationLoader.Build(_root);
        var options = configuration.Get<ClawOptions>()!;

        Assert.Equal("openai", options.Providers.DefaultProvider);
        Assert.Equal("https://api.openai.com", options.Providers.Models[0].BaseUrl);
        Assert.Equal("env-key", options.Providers.Models[0].ApiKey);
        Environment.SetEnvironmentVariable("Providers__Models__0__ApiKey", null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("Runtime__WorkspaceRoot", null);
        Environment.SetEnvironmentVariable("Providers__Models__0__ApiKey", null);
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
