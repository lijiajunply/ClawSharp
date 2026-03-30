using ClawSharp.Lib.Core;

namespace ClawSharp.Lib.Skills;

/// <summary>
/// 表示从 Markdown frontmatter 解析得到的 skill 定义。
/// </summary>
/// <param name="Id">skill 唯一标识。</param>
/// <param name="Name">skill 显示名称。</param>
/// <param name="Description">skill 功能摘要。</param>
/// <param name="Inputs">skill 预期输入说明列表。</param>
/// <param name="Outputs">skill 预期输出说明列表。</param>
/// <param name="Dependencies">skill 依赖的其它 skill 标识列表。</param>
/// <param name="RequiredTools">skill 运行所需工具列表。</param>
/// <param name="RequiredMcpServers">skill 运行所需 MCP server 列表。</param>
/// <param name="Entry">skill 主入口文件或说明入口。</param>
/// <param name="Version">skill 定义版本。</param>
/// <param name="Body">frontmatter 之后的 Markdown 正文。</param>
public sealed record SkillDefinition(
    string Id,
    string Name,
    string Description,
    IReadOnlyList<string> Inputs,
    IReadOnlyList<string> Outputs,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<string> RequiredTools,
    IReadOnlyList<string> RequiredMcpServers,
    string Entry,
    string Version,
    string Body,
    DynamicSourceType Source = DynamicSourceType.BuiltIn,
    string? OriginalId = null,
    string? SourcePath = null)
{
    /// <summary>
    /// 校验 skill 定义是否包含运行时必需字段。
    /// </summary>
    /// <exception cref="ValidationException">当必需字段为空时抛出。</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) ||
            string.IsNullOrWhiteSpace(Name) ||
            string.IsNullOrWhiteSpace(Description) ||
            string.IsNullOrWhiteSpace(Entry) ||
            string.IsNullOrWhiteSpace(Version))
        {
            throw new ValidationException("Skill definition is missing one or more required fields.");
        }
    }
}

/// <summary>
/// 提供 skill 定义的加载来源。
/// </summary>
public interface ISkillDefinitionStore
{
    /// <summary>
    /// 加载所有可用的 skill 定义。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>已解析的 skill 定义列表。</returns>
    Task<IReadOnlyList<SkillDefinition>> LoadAllAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 提供 skill 定义的内存注册表访问能力。
/// </summary>
public interface ISkillRegistry
{
    /// <summary>
    /// 从底层 store 重新加载 skill 定义并重建索引。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    Task ReloadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取当前注册表中的全部 skill 定义。
    /// </summary>
    /// <returns>按当前加载状态返回的 skill 集合。</returns>
    IReadOnlyCollection<SkillDefinition> GetAll();

    /// <summary>
    /// 按标识获取单个 skill 定义。
    /// </summary>
    /// <param name="id">skill 标识。</param>
    /// <returns>匹配的 skill 定义。</returns>
    /// <exception cref="KeyNotFoundException">当指定 skill 不存在时抛出。</exception>
    SkillDefinition Get(string id);
}
