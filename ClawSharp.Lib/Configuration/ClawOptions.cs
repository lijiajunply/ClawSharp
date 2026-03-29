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

    public WorkspacePolicy WorkspacePolicy { get; set; } = WorkspacePolicy.CreateDefault();
}

public sealed class RuntimeOptions
{
    public string WorkspaceRoot { get; set; } = Directory.GetCurrentDirectory();

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
