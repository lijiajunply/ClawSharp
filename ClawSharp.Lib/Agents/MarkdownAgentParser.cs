using ClawSharp.Lib.Core;
using ClawSharp.Lib.Tools;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ClawSharp.Lib.Agents;

/// <summary>
/// 将带 YAML frontmatter 的 agent Markdown 文本解析为 <see cref="AgentDefinition"/>。
/// </summary>
public sealed class MarkdownAgentParser
{
    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// 解析单个 agent Markdown 文本。
    /// </summary>
    /// <param name="markdown">完整的 Markdown 内容，必须包含 frontmatter。</param>
    /// <returns>解析并校验后的 <see cref="AgentDefinition"/>。</returns>
    /// <exception cref="ValidationException">当 frontmatter 缺失、格式错误或必需字段为空时抛出。</exception>
    public AgentDefinition Parse(string markdown)
    {
        var (frontMatter, body) = MarkdownFrontMatter.Parse(markdown);
        var dto = _deserializer.Deserialize<AgentFrontMatter>(frontMatter)
                  ?? throw new ValidationException("Agent frontmatter is missing or invalid.");

        var definition = new AgentDefinition(
            dto.Id ?? string.Empty,
            dto.Name ?? string.Empty,
            dto.Description ?? string.Empty,
            dto.Provider ?? string.Empty,
            dto.Model ?? string.Empty,
            dto.SystemPrompt ?? string.Empty,
            dto.Tools ?? [],
            dto.Skills ?? [],
            dto.MemoryScope ?? string.Empty,
            dto.McpServers ?? [],
            dto.Permissions?.ToPermissionSet() ?? ToolPermissionSet.Empty,
            dto.Version ?? string.Empty,
            body.Trim())
        {
            HasExplicitTools = dto.Tools != null
        };

        definition.Validate();
        return definition;
    }

    private sealed class AgentFrontMatter
    {
        public string? Id { get; init; }
        public string? Name { get; init; }
        public string? Description { get; init; }
        public string? Provider { get; init; }
        public string? Model { get; init; }
        public string? SystemPrompt { get; init; }
        public List<string>? Tools { get; init; }
        public List<string>? Skills { get; init; }
        public string? MemoryScope { get; init; }
        public List<string>? McpServers { get; init; }
        public AgentPermissionFrontMatter? Permissions { get; init; }
        public string? Version { get; init; }
    }

    private sealed class AgentPermissionFrontMatter
    {
        public List<string>? Capabilities { get; init; }
        public List<string>? AllowedReadRoots { get; init; }
        public List<string>? AllowedWriteRoots { get; init; }
        public List<string>? AllowedCommands { get; init; }
        public bool RequireApproval { get; init; }
        public bool ReadOnlyFileSystem { get; init; }
        public int? TimeoutSeconds { get; init; }
        public int? MaxOutputLength { get; init; }

        public ToolPermissionSet ToPermissionSet()
        {
            var capabilities = ToolCapability.None;
            foreach (var capability in Capabilities ?? [])
            {
                if (ToolCapabilityParser.TryParse(capability, out var parsed))
                {
                    capabilities |= parsed;
                }
            }

            return new ToolPermissionSet(
                capabilities,
                AllowedReadRoots ?? [],
                AllowedWriteRoots ?? [],
                AllowedCommands ?? [],
                RequireApproval,
                ReadOnlyFileSystem,
                TimeoutSeconds,
                MaxOutputLength);
        }
    }
}

internal static class MarkdownFrontMatter
{
    public static (string FrontMatter, string Body) Parse(string markdown)
    {
        using var reader = new StringReader(markdown);
        var firstLine = reader.ReadLine();
        if (firstLine != "---")
        {
            throw new ValidationException("Markdown frontmatter must start with ---.");
        }

        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            if (line == "---")
            {
                return (string.Join(Environment.NewLine, lines), reader.ReadToEnd());
            }

            lines.Add(line);
        }

        throw new ValidationException("Markdown frontmatter closing delimiter was not found.");
    }
}
