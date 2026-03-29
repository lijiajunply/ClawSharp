using ClawSharp.Lib.Core;
using ClawSharp.Lib.Tools;

namespace ClawSharp.Lib.Agents;

/// <summary>
/// 表示从 Markdown frontmatter 解析得到的 agent 定义。
/// </summary>
/// <param name="Id">agent 唯一标识。</param>
/// <param name="Name">agent 显示名称。</param>
/// <param name="Description">agent 功能摘要。</param>
/// <param name="Model">agent 指定的模型名；为空时由 provider 默认值补足。</param>
/// <param name="SystemPrompt">发送给模型的系统提示词。</param>
/// <param name="Tools">允许 agent 使用的工具名列表；为空表示不额外收窄授权工具。</param>
/// <param name="Skills">运行时需要装配的 skill 标识列表。</param>
/// <param name="MemoryScope">agent 写入与检索记忆时使用的 scope 名称。</param>
/// <param name="McpServers">agent 运行前需要连接的 MCP server 名称列表。</param>
/// <param name="Permissions">agent 自身声明的工具权限边界。</param>
/// <param name="Version">agent 定义版本。</param>
/// <param name="Body">frontmatter 之后的 Markdown 正文。</param>
public sealed record AgentDefinition(
    string Id,
    string Name,
    string Description,
    string Model,
    string SystemPrompt,
    IReadOnlyList<string> Tools,
    IReadOnlyList<string> Skills,
    string MemoryScope,
    IReadOnlyList<string> McpServers,
    ToolPermissionSet Permissions,
    string Version,
    string Body)
{
    /// <summary>
    /// 校验 agent 定义是否包含运行时必需字段。
    /// </summary>
    /// <exception cref="ValidationException">当必需字段为空时抛出。</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) ||
            string.IsNullOrWhiteSpace(Name) ||
            string.IsNullOrWhiteSpace(Description) ||
            string.IsNullOrWhiteSpace(Model) ||
            string.IsNullOrWhiteSpace(SystemPrompt) ||
            string.IsNullOrWhiteSpace(MemoryScope) ||
            string.IsNullOrWhiteSpace(Version))
        {
            throw new ValidationException("Agent definition is missing one or more required fields.");
        }
    }
}

/// <summary>
/// 提供 agent 定义的加载来源。
/// </summary>
public interface IAgentDefinitionStore
{
    /// <summary>
    /// 加载所有可用的 agent 定义。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>已解析的 agent 定义列表。</returns>
    Task<IReadOnlyList<AgentDefinition>> LoadAllAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 提供 agent 定义的内存注册表访问能力。
/// </summary>
public interface IAgentRegistry
{
    /// <summary>
    /// 从底层 store 重新加载 agent 定义并重建索引。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    Task ReloadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取当前注册表中的全部 agent 定义。
    /// </summary>
    /// <returns>按当前加载状态返回的 agent 集合。</returns>
    IReadOnlyCollection<AgentDefinition> GetAll();

    /// <summary>
    /// 按标识获取单个 agent 定义。
    /// </summary>
    /// <param name="id">agent 标识。</param>
    /// <returns>匹配的 agent 定义。</returns>
    /// <exception cref="KeyNotFoundException">当指定 agent 不存在时抛出。</exception>
    AgentDefinition Get(string id);
}
