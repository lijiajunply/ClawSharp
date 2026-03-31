using System.Text.RegularExpressions;
using ClawSharp.Lib.Core;

namespace ClawSharp.Lib.Projects;

/// <summary>
/// 描述项目模板中的一个空目录定义。
/// </summary>
/// <param name="RelativePath">相对于项目根目录的目录路径。</param>
public sealed record ProjectDirectoryTemplate(string RelativePath);

/// <summary>
/// 描述项目模板中的一个文件定义。
/// </summary>
/// <param name="RelativePath">相对于项目根目录的文件路径。</param>
/// <param name="Content">模板文件内容。</param>
public sealed record ProjectFileTemplate(string RelativePath, string Content);

/// <summary>
/// 描述一个可用于生成项目的模板定义。
/// </summary>
/// <param name="Id">模板唯一标识，例如 <c>paper</c>。</param>
/// <param name="Name">模板展示名称。</param>
/// <param name="Description">模板说明。</param>
/// <param name="Version">模板版本。</param>
/// <param name="Directories">需要创建的空目录定义。</param>
/// <param name="Files">需要生成的文件定义。</param>
/// <param name="DefaultVariables">模板内置的默认变量。</param>
/// <param name="ReadmeAppendix">附加到统一 README 骨架后的 Markdown 内容。</param>
public sealed record ProjectTemplateDefinition(
    string Id,
    string Name,
    string Description,
    string Version,
    IReadOnlyList<ProjectDirectoryTemplate> Directories,
    IReadOnlyList<ProjectFileTemplate> Files,
    IReadOnlyDictionary<string, string> DefaultVariables,
    string ReadmeAppendix)
{
    /// <summary>
    /// 校验模板定义是否满足运行时要求。
    /// </summary>
    /// <exception cref="ValidationException">当定义缺失必需字段或包含非法路径时抛出。</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) ||
            string.IsNullOrWhiteSpace(Name) ||
            string.IsNullOrWhiteSpace(Description) ||
            string.IsNullOrWhiteSpace(Version))
        {
            throw new ValidationException("Project template definition is missing one or more required fields.");
        }

        foreach (var directory in Directories)
        {
            ValidateRelativePath(directory.RelativePath, allowFileName: false);
        }

        foreach (var file in Files)
        {
            ValidateRelativePath(file.RelativePath, allowFileName: true);
            if (string.Equals(file.RelativePath, "README.md", StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationException("Project templates must not declare README.md directly; use the template body as README appendix.");
            }
        }
    }

    internal static void ValidateRelativePath(string path, bool allowFileName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ValidationException("Template relative paths must not be empty.");
        }

        if (Path.IsPathRooted(path))
        {
            throw new ValidationException($"Template path '{path}' must be relative.");
        }

        var normalized = path.Replace('\\', '/');
        if (normalized.StartsWith("/", StringComparison.Ordinal) ||
            normalized.Contains("/../", StringComparison.Ordinal) ||
            normalized.Contains("/./", StringComparison.Ordinal) ||
            normalized.EndsWith("/..", StringComparison.Ordinal) ||
            normalized.EndsWith("/.", StringComparison.Ordinal) ||
            normalized.StartsWith("../", StringComparison.Ordinal) ||
            normalized.StartsWith("./", StringComparison.Ordinal) ||
            normalized.Contains("//", StringComparison.Ordinal))
        {
            throw new ValidationException($"Template path '{path}' is not allowed.");
        }

        if (!allowFileName && Path.GetFileName(path).Contains('.'))
        {
            return;
        }
    }
}

/// <summary>
/// 控制项目创建时的覆盖行为。
/// </summary>
public enum OverwriteMode
{
    /// <summary>
    /// 当目标目录已存在时拒绝创建。
    /// </summary>
    RejectIfExists = 0
}

/// <summary>
/// 描述一次项目创建请求。
/// </summary>
/// <param name="ProjectType">项目类型标识，对应模板 <see cref="ProjectTemplateDefinition.Id"/>。</param>
/// <param name="ProjectName">项目名称。</param>
/// <param name="TargetPath">目标项目根目录；相对路径会按 workspace 根目录解析。</param>
/// <param name="Variables">调用方传入的自定义模板变量。</param>
/// <param name="OverwriteMode">目标目录已存在时的处理策略。</param>
public sealed record CreateProjectRequest(
    string ProjectType,
    string ProjectName,
    string TargetPath,
    IReadOnlyDictionary<string, string>? Variables = null,
    OverwriteMode OverwriteMode = OverwriteMode.RejectIfExists)
{
    /// <summary>
    /// 校验请求是否包含必要字段。
    /// </summary>
    /// <exception cref="ValidationException">当请求字段为空时抛出。</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProjectType) ||
            string.IsNullOrWhiteSpace(ProjectName) ||
            string.IsNullOrWhiteSpace(TargetPath))
        {
            throw new ValidationException("Project creation request is missing one or more required fields.");
        }
    }
}

/// <summary>
/// 描述一次项目创建成功后的结果。
/// </summary>
/// <param name="ProjectType">使用的模板类型。</param>
/// <param name="ProjectName">项目名称。</param>
/// <param name="ProjectRootPath">最终生成的项目根目录。</param>
/// <param name="CreatedDirectories">实际创建的目录绝对路径列表。</param>
/// <param name="CreatedFiles">实际创建的文件绝对路径列表。</param>
public sealed record CreateProjectResult(
    string ProjectType,
    string ProjectName,
    string ProjectRootPath,
    IReadOnlyList<string> CreatedDirectories,
    IReadOnlyList<string> CreatedFiles);

/// <summary>
/// 提供项目模板的加载来源。
/// </summary>
public interface IProjectTemplateStore
{
    /// <summary>
    /// 加载全部可用项目模板。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>模板集合。</returns>
    Task<IReadOnlyList<ProjectTemplateDefinition>> LoadAllAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 提供项目脚手架生成能力。
/// </summary>
public interface IProjectScaffolder
{
    /// <summary>
    /// 列出当前可用的项目模板。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>模板集合。</returns>
    Task<IReadOnlyList<ProjectTemplateDefinition>> ListTemplatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 基于指定模板创建项目。
    /// </summary>
    /// <param name="request">项目创建请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成功时返回创建结果，失败时携带错误描述。</returns>
    Task<OperationResult<CreateProjectResult>> CreateProjectAsync(
        CreateProjectRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 将 SpecKit 治理结构应用到指定项目目录。
    /// </summary>
    Task<OperationResult<ApplySpecKitResult>> ApplySpecKitAsync(
        string projectRoot,
        CancellationToken cancellationToken = default);
}

internal static partial class ProjectTemplateRenderer
{
    private static readonly Regex VariablePattern = new("{{\\s*(?<name>[a-zA-Z0-9_.-]+)\\s*}}", RegexOptions.Compiled);

    public static string Render(string template, IReadOnlyDictionary<string, string> variables)
    {
        return VariablePattern.Replace(template, match =>
        {
            var key = match.Groups["name"].Value;
            if (!variables.TryGetValue(key, out var value))
            {
                throw new ValidationException($"Missing template variable '{key}'.");
            }

            return value;
        });
    }
}
