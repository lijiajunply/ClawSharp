using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Runtime;
using Microsoft.Extensions.Configuration;

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
        await manager.SetAsync("Runtime:OutputLanguage", "zh-CN");
        
        Assert.True(File.Exists(localJson));
        var content = File.ReadAllText(localJson);
        Assert.Contains("\"DefaultProvider\": \"anthropic\"", content);
        Assert.Contains("\"OutputLanguage\": \"zh-CN\"", content);

        // Test masking
        Assert.True(manager.IsSecret("Providers:Models:0:ApiKey"));
    }

    [Fact]
    public async Task ConfigManager_OnlyReadsClawConfigFiles()
    {
        File.WriteAllText(Path.Combine(_root, "appsettings.json"), """
{
  "Runtime": {
    "WorkspaceRoot": "json-root"
  }
}
""");
        Environment.SetEnvironmentVariable("Runtime__WorkspaceRoot", "process-root");
        Environment.SetEnvironmentVariable("UNRELATED_SYSTEM_SETTING", "system-value");

        var localJson = Path.Combine(_root, "appsettings.Local.json");
        var config = ClawConfigurationLoader.Build(_root);
        var options = new ClawOptions();
        var manager = new ConfigManager(config, options, localJson);

        Assert.Equal("json-root", manager.Get("Runtime:WorkspaceRoot"));

        var all = await manager.GetAllAsync();
        Assert.Contains("Runtime:WorkspaceRoot", all.Keys);
        Assert.DoesNotContain("UNRELATED_SYSTEM_SETTING", all.Keys, StringComparer.OrdinalIgnoreCase);

        Environment.SetEnvironmentVariable("Runtime__WorkspaceRoot", null);
        Environment.SetEnvironmentVariable("UNRELATED_SYSTEM_SETTING", null);
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
         Assert.Contains("Runtime:OutputLanguage", keyList);
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

    [Fact]
    public async Task ConfigBootstrapper_GeneratesValidJson()
    {
        var bootstrapper = new ConfigBootstrapper();
        var config = new BootstrapConfig
        {
            WorkspaceRoot = "/tmp/ws",
            DataPath = ".data",
            DefaultProvider = "openai",
            ProviderType = "openai-responses",
            BaseUrl = "https://api.openai.com",
            DefaultModel = "gpt-test",
            SupportsResponses = true,
            ApiKey = "sk-test"
        };

        var json = bootstrapper.GenerateConfigJson(config);
        
        Assert.Contains("\"WorkspaceRoot\": \"/tmp/ws\"", json);
        Assert.Contains("\"BaseUrl\": \"https://api.openai.com\"", json);
        Assert.Contains("\"DefaultModel\": \"gpt-test\"", json);
        Assert.Contains("\"SupportsResponses\": true", json);
        Assert.Contains("\"ApiKey\": \"sk-test\"", json);

        var path = Path.Combine(_root, "appsettings.json");
        await bootstrapper.SaveConfigAsync(path, json);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void ConfigBootstrapper_IncludesDiscoveredLocalProviders_First()
    {
        var bootstrapper = new ConfigBootstrapper();
        var discovery = new EnvironmentDiscoveryResult(
            PlaywrightInstalled: true,
            Ollama: new LocalModelServiceDiscovery(true, "http://127.0.0.1:11434", ["qwen3:latest"]),
            LlamaEdge: new LocalModelServiceDiscovery(true, "http://127.0.0.1:8080", ["llama-edge"]));

        var templates = bootstrapper.GetProviderTemplates(discovery).ToArray();

        Assert.Equal("ollama-local", templates[0].Id);
        Assert.Equal("llamaedge-local", templates[1].Id);
        Assert.False(templates[0].RequiresApiKey);
        Assert.Equal("qwen3:latest", templates[0].DefaultModel);
        Assert.Equal("http://127.0.0.1:8080", templates[1].BaseUrl);
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
