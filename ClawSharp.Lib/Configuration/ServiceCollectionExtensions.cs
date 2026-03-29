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

/// <summary>
/// 描述 <c>AddClawSharp(...)</c> 的构建参数。
/// </summary>
public sealed class ClawBuilder
{
    internal Dictionary<string, string?> Overrides { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 解析配置文件与相对路径时使用的基础目录。默认是当前工作目录。
    /// </summary>
    public string BasePath { get; set; } = Directory.GetCurrentDirectory();

    /// <summary>
    /// 可选的预建配置对象。设置后将跳过默认的配置文件加载逻辑。
    /// </summary>
    public IConfiguration? Configuration { get; set; }

    /// <summary>
    /// 以最高优先级覆盖某个配置键。
    /// </summary>
    /// <param name="key">配置键，遵循 <c>Section:Child</c> 风格。</param>
    /// <param name="value">配置值；可为 <see langword="null"/>。</param>
    public void Override(string key, string? value) => Overrides[key] = value;
}

/// <summary>
/// ClawSharp 服务注册扩展。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 向依赖注入容器注册 ClawSharp.Lib 的默认实现。
    /// </summary>
    /// <param name="services">目标服务集合。</param>
    /// <param name="configure">可选的构建器回调，用于设置 <see cref="ClawBuilder.BasePath"/>、提供现成配置或追加运行时覆盖项。</param>
    /// <returns>同一个 <see cref="IServiceCollection"/>，便于链式注册。</returns>
    /// <remarks>
    /// 配置优先级从低到高依次为：默认 JSON 文件、local JSON、<c>.env</c>、环境变量以及 <see cref="ClawBuilder.Override"/> 传入的覆盖项。
    /// 如果直接提供 <see cref="ClawBuilder.Configuration"/>，则该配置对象会替代上述自动加载流程。
    /// </remarks>
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
        services.AddSingleton<ClawSqliteDbContextFactory>();
        services.AddSingleton<DuckDbConnectionFactory>();
        services.AddSingleton<ISessionRecordRepository, EfSessionRecordRepository>();
        services.AddSingleton<IPromptMessageRepository, EfPromptMessageRepository>();
        services.AddSingleton<ISessionEventRepository, EfSessionEventRepository>();
        services.AddSingleton<IDuckDbAnalyticsProjector, DuckDbAnalyticsProjector>();
        services.AddSingleton<ISessionSerializer, JsonSessionSerializer>();
        services.AddSingleton<ISessionStore>(serviceProvider =>
            new SqliteSessionStore(serviceProvider.GetRequiredService<ISessionRecordRepository>()));
        services.AddSingleton<IPromptHistoryStore>(serviceProvider =>
            new SqlitePromptHistoryStore(serviceProvider.GetRequiredService<IPromptMessageRepository>()));
        services.AddSingleton<ISessionEventStore>(serviceProvider =>
            new SqliteSessionEventStore(serviceProvider.GetRequiredService<ISessionEventRepository>()));
        services.AddSingleton<ISessionAnalyticsService>(serviceProvider =>
            options.Databases.DuckDb.Enabled
                ? new DuckDbSessionAnalyticsService(
                    options,
                    serviceProvider.GetRequiredService<IDuckDbAnalyticsProjector>(),
                    serviceProvider.GetRequiredService<DuckDbConnectionFactory>())
                : new NullSessionAnalyticsService());
        services.AddSingleton<ISessionManager, SessionManager>();
        services.AddSingleton<IProviderHttpClientFactory, DefaultProviderHttpClientFactory>();
        services.AddSingleton<IModelProvider, StubModelProvider>();
        services.AddSingleton<IModelProvider, OpenAiResponsesModelProvider>();
        services.AddSingleton<IModelProvider, OpenAiCompatibleChatModelProvider>();
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
