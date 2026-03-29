using ClawSharp.Lib.Mcp;
using ClawSharp.Lib.Tools;

namespace ClawSharp.Lib.Configuration;

public sealed class ClawOptions
{
    public RuntimeOptions Runtime { get; set; } = new();

    public AgentOptions Agents { get; set; } = new();

    public ToolOptions Tools { get; set; } = new();

    public McpOptions Mcp { get; set; } = new();

    public MemoryOptions Memory { get; set; } = new();

    public EmbeddingOptions Embedding { get; set; } = new();

    public SessionOptions Sessions { get; set; } = new();

    public HistoryOptions History { get; set; } = new();

    public ProviderOptions Providers { get; set; } = new();

    public WorkerOptions Worker { get; set; } = new();

    public WorkspacePolicy WorkspacePolicy { get; set; } = WorkspacePolicy.CreateDefault();
}

public sealed class RuntimeOptions
{
    public string WorkspaceRoot { get; set; } = Directory.GetCurrentDirectory();

    public string DataPath { get; set; } = ".clawsharp";

    public string? AgentWorkerCommand { get; set; }

    public string AgentWorkerArguments { get; set; } = string.Empty;

    public int DefaultTimeoutSeconds { get; set; } = 60;
}

public sealed class AgentOptions
{
    public string AgentsPath { get; set; } = "workspace/agents";

    public string SkillsPath { get; set; } = "workspace/skills";
}

public sealed class ToolOptions
{
    public ToolSecurityOptions Security { get; set; } = new();
}

public sealed class ToolSecurityOptions
{
    public bool RequireApprovalByDefault { get; set; }

    public List<string> AllowedReadRoots { get; set; } = [];

    public List<string> AllowedWriteRoots { get; set; } = [];

    public List<string> AllowedShellCommands { get; set; } = [];

    public int MaxCommandOutputLength { get; set; } = 16_384;

    public int MaxCommandTimeoutSeconds { get; set; } = 60;
}

public sealed class McpOptions
{
    public List<McpServerDefinition> Servers { get; set; } = [];
}

public sealed class MemoryOptions
{
    public int ChunkSize { get; set; } = 500;

    public int ChunkOverlap { get; set; } = 50;
}

public sealed class EmbeddingOptions
{
    public string Provider { get; set; } = "simple";

    public int Dimensions { get; set; } = 16;
}

public sealed class SessionOptions
{
    public string DatabasePath { get; set; } = ".clawsharp/clawsharp.db";

    public int MaxHistoryEntries { get; set; } = 1_000;
}

public sealed class HistoryOptions
{
    public bool RecordToolPayloads { get; set; } = true;

    public bool RecordMessageDeltas { get; set; } = true;

    public int MaxPayloadLength { get; set; } = 32_768;
}

public sealed class ProviderOptions
{
    public string DefaultProvider { get; set; } = "openai";

    public string DefaultModel { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 60;

    public List<ModelProviderOptions> Models { get; set; } = [];
}

public sealed class ModelProviderOptions
{
    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = "stub";

    public string BaseUrl { get; set; } = string.Empty;

    public string? ApiKey { get; set; }

    public string DefaultModel { get; set; } = string.Empty;

    public string? Organization { get; set; }

    public string? Project { get; set; }

    public string? RequestPath { get; set; }

    public bool SupportsResponses { get; set; }

    public bool SupportsChatCompletions { get; set; }
}

public sealed class WorkerOptions
{
    public string? Command { get; set; }

    public string Arguments { get; set; } = string.Empty;

    public int StartupTimeoutSeconds { get; set; } = 10;

    public int RpcTimeoutSeconds { get; set; } = 60;

    public bool AutoRestart { get; set; }

    public string LogLevel { get; set; } = "Information";
}
