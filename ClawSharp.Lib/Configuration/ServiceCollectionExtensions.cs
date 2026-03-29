using ClawSharp.Lib.Agents;
using ClawSharp.Lib.Memory;
using ClawSharp.Lib.Mcp;
using ClawSharp.Lib.Providers;
using ClawSharp.Lib.Runtime;
using ClawSharp.Lib.Skills;
using ClawSharp.Lib.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClawSharp.Lib.Configuration;

public sealed class ClawBuilder
{
    internal Dictionary<string, string?> Overrides { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string BasePath { get; set; } = Directory.GetCurrentDirectory();

    public IConfiguration? Configuration { get; set; }

    public void Override(string key, string? value) => Overrides[key] = value;
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddClawSharp(
        this IServiceCollection services,
        Action<ClawBuilder>? configure = null)
    {
        var builder = new ClawBuilder();
        configure?.Invoke(builder);

        var configuration = builder.Configuration ?? ClawConfigurationLoader.Build(builder.BasePath, builder.Overrides);
        var options = configuration.Get<ClawOptions>() ?? new ClawOptions();

        if (!Path.IsPathRooted(options.Runtime.WorkspaceRoot))
        {
            options.Runtime.WorkspaceRoot = Path.GetFullPath(Path.Combine(builder.BasePath, options.Runtime.WorkspaceRoot));
        }

        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(options);
        services.AddSingleton<IAgentDefinitionStore, FileSystemAgentDefinitionStore>();
        services.AddSingleton<ISkillDefinitionStore, FileSystemSkillDefinitionStore>();
        services.AddSingleton<IAgentRegistry, AgentRegistry>();
        services.AddSingleton<ISkillRegistry, SkillRegistry>();
        services.AddSingleton<IToolRegistry, ToolRegistry>();
        services.AddSingleton<IEmbeddingProvider, SimpleEmbeddingProvider>();
        services.AddSingleton<IVectorStore, InMemoryVectorStore>();
        services.AddSingleton<IMemoryScopeResolver, DefaultMemoryScopeResolver>();
        services.AddSingleton<IMemoryIndex, MemoryIndex>();
        services.AddSingleton<ISessionSerializer, JsonSessionSerializer>();
        services.AddSingleton<ISessionStore, SqliteSessionStore>();
        services.AddSingleton<IPromptHistoryStore, SqlitePromptHistoryStore>();
        services.AddSingleton<ISessionEventStore, SqliteSessionEventStore>();
        services.AddSingleton<ISessionManager, SessionManager>();
        services.AddSingleton<IModelProvider, StubModelProvider>();
        services.AddSingleton<IModelProviderRegistry, ModelProviderRegistry>();
        services.AddSingleton<IModelProviderResolver, ModelProviderResolver>();
        services.AddSingleton<IMcpServerCatalog, McpServerCatalog>();
        services.AddSingleton<IMcpClientManager, McpClientManager>();
        services.AddSingleton<IAgentWorkerClient, StdioJsonRpcAgentWorkerClient>();
        services.AddSingleton<IAgentWorkerLauncher, DefaultAgentWorkerLauncher>();
        services.AddSingleton<IClawKernel, ClawKernel>();
        services.AddSingleton<IClawRuntime, ClawRuntime>();

        services.AddSingleton<IToolExecutor, ShellRunTool>();
        services.AddSingleton<IToolExecutor, FileReadTool>();
        services.AddSingleton<IToolExecutor, FileWriteTool>();
        services.AddSingleton<IToolExecutor, FileListTool>();
        services.AddSingleton<IToolExecutor, SystemInfoTool>();
        services.AddSingleton<IToolExecutor, SystemProcessesTool>();
        services.AddSingleton<IToolExecutor, SearchTextTool>();
        services.AddSingleton<IToolExecutor, SearchFilesTool>();

        return services;
    }
}
