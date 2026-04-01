using ClawSharp.Lib.Mcp;
using ClawSharp.Lib.Tools;

namespace ClawSharp.Lib.Configuration;

/// <summary>
/// ClawSharp 的根配置对象。
/// </summary>
public sealed class ClawOptions
{
    /// <summary>
    /// 运行时级别配置，包括 workspace 根目录和默认超时。
    /// </summary>
    public RuntimeOptions Runtime { get; set; } = new();

    /// <summary>
    /// Agent 与 Skill 定义所在目录的配置。
    /// </summary>
    public AgentOptions Agents { get; set; } = new();

    /// <summary>
    /// 工具系统及其安全策略配置。
    /// </summary>
    public ToolOptions Tools { get; set; } = new();

    /// <summary>
    /// MCP server 目录配置。
    /// </summary>
    public McpOptions Mcp { get; set; } = new();

    /// <summary>
    /// 记忆切块参数配置。
    /// </summary>
    public MemoryOptions Memory { get; set; } = new();

    /// <summary>
    /// embedding 默认实现配置。
    /// </summary>
    public EmbeddingOptions Embedding { get; set; } = new();

    /// <summary>
    /// session 持久化与历史窗口配置。
    /// </summary>
    public SessionOptions Sessions { get; set; } = new();

    /// <summary>
    /// 数据库存储配置。
    /// </summary>
    public DatabaseOptions Databases { get; set; } = new();

    /// <summary>
    /// 历史事件记录配置。
    /// </summary>
    public HistoryOptions History { get; set; } = new();

    /// <summary>
    /// 模型 provider 解析与默认模型配置。
    /// </summary>
    public ProviderOptions Providers { get; set; } = new();

    /// <summary>
    /// ClawHub 远程技能目录配置。
    /// </summary>
    public HubOptions Hub { get; set; } = new();

    /// <summary>
    /// Smithery MCP 市场配置。
    /// </summary>
    public SmitheryOptions Smithery { get; set; } = new();

    /// <summary>
    /// 外部 worker 进程配置。
    /// </summary>
    public WorkerOptions Worker { get; set; } = new();

    /// <summary>
    /// 项目模板与脚手架配置。
    /// </summary>
    public ProjectOptions Projects { get; set; } = new();

    /// <summary>
    /// 多 Agent 编排与委派配置。
    /// </summary>
    public OrchestrationOptions Orchestration { get; set; } = new();

    /// <summary>
    /// workspace 级默认权限策略；会在运行时与 agent 权限取交集。
    /// </summary>
    public WorkspacePolicy WorkspacePolicy { get; set; } = WorkspacePolicy.CreateDefault();
}

/// <summary>
/// 多 Agent 编排与委派配置。
/// </summary>
public sealed class OrchestrationOptions
{
    /// <summary>
    /// 最大委派深度，防止无限递归。默认为 5。
    /// </summary>
    public int MaxDelegationDepth { get; set; } = 5;

    /// <summary>
    /// 默认编排策略，例如 "sequential", "parallel"。默认为 "sequential"。
    /// </summary>
    public string DefaultStrategy { get; set; } = "sequential";

    /// <summary>
    /// 是否自动将工作区中的所有 Agent 发现为可调用的工具。默认为 true。
    /// </summary>
    public bool AutoDiscoverAgents { get; set; } = true;
}

/// <summary>
/// 控制 runtime 基本行为的配置。
/// </summary>
public sealed class RuntimeOptions
{
    /// <summary>
    /// workspace 根目录。相对路径会在 <c>AddClawSharp(...)</c> 中按 <see cref="ClawBuilder.BasePath"/> 解析为绝对路径。
    /// </summary>
    public string WorkspaceRoot { get; set; } = Directory.GetCurrentDirectory();

    /// <summary>
    /// 运行时数据目录。默认值是 <c>.clawsharp</c>。
    /// </summary>
    public string DataPath { get; set; } = ".clawsharp";

    /// <summary>
    /// 外部 agent worker 启动命令；为空时会回退到进程内 loopback worker。
    /// </summary>
    public string? AgentWorkerCommand { get; set; }

    /// <summary>
    /// 传给外部 agent worker 的命令行参数。
    /// </summary>
    public string AgentWorkerArguments { get; set; } = string.Empty;

    /// <summary>
    /// runtime 默认超时秒数。未被更具体配置覆盖时使用。
    /// </summary>
    public int DefaultTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// ThreadSpace 内系统提示增强配置。
    /// </summary>
    public ThreadSpacePromptOptions ThreadSpacePrompt { get; set; } = new();
}

/// <summary>
/// 控制 ThreadSpace 场景下的动态系统提示增强行为。
/// </summary>
public sealed class ThreadSpacePromptOptions
{
    /// <summary>
    /// 是否启用 ThreadSpace 提示增强。默认为 true。
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 需要自动注入到系统提示中的项目文档文件名列表。
    /// </summary>
    public List<string> ProjectDocumentCandidates { get; set; } =
    [
        "README.md",
        "README",
        "AGENTS.md",
        "agents.md",
        "CLAUDE.md",
        "claude.md",
        "CLAUDE",
        "claude",
        "GEMINI.md",
        "gemini.md",
        "GEMINI",
        "gemini"
    ];

    /// <summary>
    /// 单个项目文档最多注入的字符数。
    /// </summary>
    public int MaxDocumentChars { get; set; } = 4_000;

    /// <summary>
    /// 所有项目文档合计最多注入的字符数。
    /// </summary>
    public int MaxCombinedDocumentChars { get; set; } = 10_000;
}

/// <summary>
/// Agent 和 Skill 定义目录配置。
/// </summary>
public sealed class AgentOptions
{
    /// <summary>
    /// 默认使用的 agent 标识。
    /// </summary>
    public string? DefaultAgentId { get; set; }

    /// <summary>
    /// agent 定义目录。相对路径默认解析到 workspace 下的 <c>workspace/agents</c>。
    /// </summary>
    public string AgentsPath { get; set; } = "workspace/agents";

    /// <summary>
    /// skill 定义目录。相对路径默认解析到 workspace 下的 <c>workspace/skills</c>。
    /// </summary>
    public string SkillsPath { get; set; } = "workspace/skills";
}

/// <summary>
/// 工具系统的顶层配置。
/// </summary>
public sealed class ToolOptions
{
    /// <summary>
    /// 工具安全策略配置。
    /// </summary>
    public ToolSecurityOptions Security { get; set; } = new();

    /// <summary>
    /// 联网搜索工具配置。
    /// </summary>
    public SearchOptions Search { get; set; } = new();
}

/// <summary>
/// 联网搜索工具的详细配置。
/// </summary>
public sealed class SearchOptions
{
    /// <summary>
    /// 搜索提供商，例如 <c>duckduckgo</c> (默认)、<c>google</c>、<c>bing</c>、<c>tavily</c>。
    /// </summary>
    public string Provider { get; set; } = "duckduckgo";

    /// <summary>
    /// 搜索 API Key（如果提供商需要）。
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// 默认搜索结果数量限制，默认为 5。
    /// </summary>
    public int DefaultLimit { get; set; } = 5;
}

/// <summary>
/// 控制工具权限边界、审批策略和执行限制的配置。
/// </summary>
public sealed class ToolSecurityOptions
{
    /// <summary>
    /// 为 <see langword="true"/> 时，命中的工具调用默认进入审批流程。
    /// </summary>
    public bool RequireApprovalByDefault { get; set; }

    /// <summary>
    /// 允许读取的根路径列表。为空表示不额外限制读取根目录。
    /// </summary>
    public List<string> AllowedReadRoots { get; set; } = [];

    /// <summary>
    /// 允许写入的根路径列表。为空表示不额外限制写入根目录。
    /// </summary>
    public List<string> AllowedWriteRoots { get; set; } = [];

    /// <summary>
    /// 允许执行的 shell 命令白名单。为空表示不按命令名做额外限制。
    /// </summary>
    public List<string> AllowedShellCommands { get; set; } = [];

    /// <summary>
    /// 单次命令最多保留的输出长度，默认值为 16384 个字符。
    /// </summary>
    public int MaxCommandOutputLength { get; set; } = 16_384;

    /// <summary>
    /// 单次命令执行最大时长，默认值为 60 秒。
    /// </summary>
    public int MaxCommandTimeoutSeconds { get; set; } = 60;
}

/// <summary>
/// MCP server 配置集合。
/// </summary>
public sealed class McpOptions
{
    /// <summary>
    /// 可用 MCP server 定义列表。
    /// </summary>
    public List<McpServerDefinition> Servers { get; set; } = [];

    /// <summary>
    /// MCP 连接池配置。
    /// </summary>
    public McpPoolOptions Pool { get; set; } = new();
}

/// <summary>
/// MCP 连接池详细配置。
/// </summary>
public sealed class McpPoolOptions
{
    /// <summary>
    /// 空闲连接的回收阈值（秒）。默认 10 分钟。
    /// </summary>
    public int IdleTtlSeconds { get; set; } = 600;

    /// <summary>
    /// 连接池允许保留的最大连接数。
    /// </summary>
    public int MaxPoolSize { get; set; } = 8;
}

/// <summary>
/// 控制记忆存储与 RAG 检索行为的配置。
/// </summary>
public sealed class MemoryOptions
{
    /// <summary>
    /// 向量存储实现类型。例如 <c>simple</c> (内存)、<c>sqlite-vss</c>。
    /// </summary>
    public string VectorStoreType { get; set; } = "sqlite-vss";

    /// <summary>
    /// 单个记忆块的最大字符数。默认值为 500。
    /// </summary>
    public int ChunkSize { get; set; } = 500;

    /// <summary>
    /// 相邻记忆块之间的重叠字符数。默认值为 50。
    /// </summary>
    public int ChunkOverlap { get; set; } = 50;

    /// <summary>
    /// 自动化 RAG 检索时返回的最大上下文片段数量。默认值为 5。
    /// </summary>
    public int TopK { get; set; } = 5;
}

/// <summary>
/// embedding 默认实现配置。
/// </summary>
public sealed class EmbeddingOptions
{
    /// <summary>
    /// embedding 类型。例如 <c>cloud</c> (OpenAI)、<c>local</c> (ONNX)。
    /// </summary>
    public string Type { get; set; } = "cloud";

    /// <summary>
    /// embedding provider 名称。默认实现使用 <c>simple</c>，可设为 <c>openai</c>。
    /// </summary>
    public string Provider { get; set; } = "simple";

    /// <summary>
    /// 使用的具体模型名。对于 OpenAI，默认为 <c>text-embedding-3-small</c>。
    /// 对于本地模型，指定模型标识符或路径。
    /// </summary>
    public string Model { get; set; } = "text-embedding-3-small";

    /// <summary>
    /// embedding API 基础地址；为空时由 provider 选择默认端点。
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// embedding 专属 API key；为空时可能回退到全局 provider 配置。
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// embedding 向量维度。默认值为 1536 (对应 text-embedding-3-small)。
    /// </summary>
    public int Dimensions { get; set; } = 1536;
}

/// <summary>
/// session 存储相关配置。
/// </summary>
public sealed class SessionOptions
{
    /// <summary>
    /// SQLite 数据库路径。相对路径默认落到 workspace 下的 <c>.clawsharp/clawsharp.db</c>。
    /// 兼容旧配置，优先级低于 <see cref="DatabaseOptions.Sqlite"/>。
    /// </summary>
    public string DatabasePath { get; set; } = ".clawsharp/clawsharp.db";

    /// <summary>
    /// 单个 session 可保留的最大历史条数。当前默认值为 1000。
    /// </summary>
    public int MaxHistoryEntries { get; set; } = 1_000;
}

/// <summary>
/// 数据库存储配置。
/// </summary>
public sealed class DatabaseOptions
{
    /// <summary>
    /// SQLite 主存储配置。
    /// </summary>
    public SqliteDatabaseOptions Sqlite { get; set; } = new();

    /// <summary>
    /// DuckDB 分析存储配置。
    /// </summary>
    public DuckDbDatabaseOptions DuckDb { get; set; } = new();
}

/// <summary>
/// SQLite 主存储配置。
/// </summary>
public sealed class SqliteDatabaseOptions
{
    /// <summary>
    /// 主数据库路径。相对路径默认落到 workspace 下的 <c>.clawsharp/clawsharp.db</c>。
    /// </summary>
    public string DatabasePath { get; set; } = string.Empty;
}

/// <summary>
/// DuckDB 分析存储配置。
/// </summary>
public sealed class DuckDbDatabaseOptions
{
    /// <summary>
    /// 是否启用 DuckDB 分析层。
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 分析数据库路径。相对路径默认落到 workspace 下的 <c>.clawsharp/analytics.duckdb</c>。
    /// </summary>
    public string DatabasePath { get; set; } = ".clawsharp/analytics.duckdb";
}

/// <summary>
/// 控制 prompt 历史和事件落盘细节的配置。
/// </summary>
public sealed class HistoryOptions
{
    /// <summary>
    /// 为 <see langword="true"/> 时记录工具 payload 到历史存储。
    /// </summary>
    public bool RecordToolPayloads { get; set; } = true;

    /// <summary>
    /// 为 <see langword="true"/> 时记录流式文本增量事件。
    /// </summary>
    public bool RecordMessageDeltas { get; set; } = true;

    /// <summary>
    /// 单条历史 payload 的最大长度。默认值为 32768 个字符。
    /// </summary>
    public int MaxPayloadLength { get; set; } = 32_768;
}

/// <summary>
/// 模型 provider 解析的顶层配置。
/// </summary>
public sealed class ProviderOptions
{
    /// <summary>
    /// 默认 provider 名称，需要与 <see cref="Models"/> 中某一项的 <see cref="ModelProviderOptions.Name"/> 对应。
    /// </summary>
    public string DefaultProvider { get; set; } = "openai";

    /// <summary>
    /// 当 agent 未指定模型且 provider 也未给出默认模型时使用的全局模型名。
    /// </summary>
    public string DefaultModel { get; set; } = string.Empty;

    /// <summary>
    /// provider 调用默认超时秒数。
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// 可用 provider 配置列表。
    /// </summary>
    public List<ModelProviderOptions> Models { get; set; } = [];
}

/// <summary>
/// ClawHub 配置。
/// </summary>
public sealed class HubOptions
{
    /// <summary>
    /// 是否启用 ClawHub 集成。
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// ClawHub API 基础地址。
    /// </summary>
    public string BaseUrl { get; set; } = "https://hub.claw.dev";

    /// <summary>
    /// 远程请求超时时间，单位秒。
    /// </summary>
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// Skill 安装根目录。支持使用 ~ 表示用户主目录。
    /// </summary>
    public string InstallRoot { get; set; } = "~/.skills";
}

/// <summary>
/// Smithery 市场配置。
/// </summary>
public sealed class SmitheryOptions
{
    /// <summary>
    /// 是否启用 Smithery 集成。
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Smithery API 基础地址。
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.smithery.ai";

    /// <summary>
    /// Smithery API Key。部分接口可能需要。
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// 远程请求超时时间，单位秒。
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// 单个模型 provider 的连接与能力配置。
/// </summary>
public sealed class ModelProviderOptions
{
    /// <summary>
    /// provider 配置名，用于运行时选择 provider。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// provider 实现类型，例如 <c>stub</c>、<c>openai-responses</c>。
    /// </summary>
    public string Type { get; set; } = "stub";

    /// <summary>
    /// provider 基础地址。
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// provider API key。远程 provider 缺失该值时会在运行时失败。
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// provider 默认模型名。agent 未指定模型时优先使用该值。
    /// </summary>
    public string DefaultModel { get; set; } = string.Empty;

    /// <summary>
    /// 可选的组织标识，会写入 OpenAI 风格请求头。
    /// </summary>
    public string? Organization { get; set; }

    /// <summary>
    /// 可选的项目标识，会写入 OpenAI 风格请求头。
    /// </summary>
    public string? Project { get; set; }

    /// <summary>
    /// 自定义请求路径；为空时由具体 provider 选择默认端点。
    /// </summary>
    public string? RequestPath { get; set; }

    /// <summary>
    /// 标识该 provider 配置是否支持 Responses API 语义。
    /// </summary>
    public bool SupportsResponses { get; set; }

    /// <summary>
    /// 标识该 provider 配置是否支持 Chat Completions 语义。
    /// </summary>
    public bool SupportsChatCompletions { get; set; }
}

/// <summary>
/// 外部 worker 进程配置。
/// </summary>
public sealed class WorkerOptions
{
    /// <summary>
    /// 外部 worker 启动命令；为空时不会启动独立进程。
    /// </summary>
    public string? Command { get; set; }

    /// <summary>
    /// 外部 worker 的命令行参数。
    /// </summary>
    public string Arguments { get; set; } = string.Empty;

    /// <summary>
    /// worker 启动超时秒数，默认值为 10。
    /// </summary>
    public int StartupTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// worker RPC 调用超时秒数，默认值为 60。
    /// </summary>
    public int RpcTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// 为 <see langword="true"/> 时允许 future 实现自动重启 worker。
    /// </summary>
    public bool AutoRestart { get; set; }

    /// <summary>
    /// worker 日志级别提示，默认值为 <c>Information</c>。
    /// </summary>
    public string LogLevel { get; set; } = "Information";
}

/// <summary>
/// 项目模板与脚手架配置。
/// </summary>
public sealed class ProjectOptions
{
    /// <summary>
    /// 项目模板目录。相对路径默认解析到 workspace 根目录下。
    /// </summary>
    public string TemplatesPath { get; set; } = "workspace/project-templates";

    /// <summary>
    /// SpecKit 根目录。相对路径默认解析到 workspace 根目录下。
    /// </summary>
    public string SpecKitPath { get; set; } = ".specify";
}
