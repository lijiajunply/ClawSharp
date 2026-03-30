using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClawSharp.Lib.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ClawSharp.Lib.Tests;

public sealed class ConfigurationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "claw-config-tests", Guid.NewGuid().ToString("N"));

    public ConfigurationTests()
    {
        Directory.CreateDirectory(_root);
        // Ensure the LocalJsonPath in ConfigManager can be controlled or points to a temp location.
        // Current ConfigManager uses AppDomain.CurrentDomain.BaseDirectory.
        // For testing, we might need to modify ConfigManager to take path as an option or use a different approach.
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

    [Fact]
    public async Task ConfigManager_CanSetAndPersist()
    {
        var localJson = Path.Combine(_root, "appsettings.Local.json");
        var config = ClawConfigurationLoader.Build(_root);
        var options = new ClawOptions();
        var manager = new ConfigManager(config, options, localJson);

        await manager.SetAsync("Providers:DefaultProvider", "anthropic");
        
        Assert.True(File.Exists(localJson));
        var content = File.ReadAllText(localJson);
        Assert.Contains("\"DefaultProvider\": \"anthropic\"", content);

        // Test masking
        Assert.True(manager.IsSecret("Providers:Models:0:ApiKey"));
    }

    [Fact]
    public async Task ConfigManager_CanSetAndMaskSecrets()
    {
        var config = ClawConfigurationLoader.Build(_root);
        var options = new ClawOptions();
        var manager = new ConfigManager(config, options);

        // Before that, let's test IsSecret logic
        Assert.True(manager.IsSecret("Providers:Models:0:ApiKey"));
        Assert.True(manager.IsSecret("SomeService:Token"));
        Assert.False(manager.IsSecret("Runtime:WorkspaceRoot"));
    }

    [Fact]
    public async Task ConfigManager_Reflection_DiscoversSupportedKeys()
    {
         var config = ClawConfigurationLoader.Build(_root);
         var options = new ClawOptions();
         var manager = new ConfigManager(config, options);

         var keys = await manager.GetSupportedKeysAsync();
         var keyList = keys.ToList();

         Assert.Contains("Runtime:WorkspaceRoot", keyList);
         Assert.Contains("Providers:DefaultProvider", keyList);
         Assert.Contains("Agents:AgentsPath", keyList);
    }
[Fact]
public async Task ConfigManager_CanReset()
{
    var localJson = Path.Combine(_root, "appsettings.Local.json");
    var config = ClawConfigurationLoader.Build(_root);
    var options = new ClawOptions();
    var manager = new ConfigManager(config, options, localJson);

    await manager.SetAsync("Runtime:WorkspaceRoot", "custom-root");
    Assert.True(File.Exists(localJson));

    await manager.ResetAsync(key: "Runtime:WorkspaceRoot");
    var content = File.ReadAllText(localJson);
    Assert.DoesNotContain("\"WorkspaceRoot\": \"custom-root\"", content);

    await manager.SetAsync("Runtime:WorkspaceRoot", "custom-root");
    await manager.ResetAsync(all: true);
    Assert.False(File.Exists(localJson));
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
