using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Core;

namespace ClawSharp.Lib.Projects;

/// <summary>
/// 从文件系统模板目录加载项目模板定义。
/// </summary>
public sealed class FileSystemProjectTemplateStore(
    ClawOptions options,
    MarkdownProjectTemplateParser parser) : IProjectTemplateStore
{
    private readonly string _templatesPath = ResolveTemplatesPath(options);

    /// <inheritdoc />
    public Task<IReadOnlyList<ProjectTemplateDefinition>> LoadAllAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_templatesPath))
        {
            return Task.FromResult<IReadOnlyList<ProjectTemplateDefinition>>([]);
        }

        var templates = Directory.EnumerateDirectories(_templatesPath, "*", SearchOption.TopDirectoryOnly)
            .Select(parser.ParseDirectory)
            .OrderBy(template => template.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var duplicateIds = templates
            .GroupBy(template => template.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicateIds.Length > 0)
        {
            throw new ValidationException($"Duplicate project template ids were found: {string.Join(", ", duplicateIds)}.");
        }

        return Task.FromResult<IReadOnlyList<ProjectTemplateDefinition>>(templates);
    }

    private static string ResolveTemplatesPath(ClawOptions options)
    {
        var path = options.Projects.TemplatesPath;
        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        return Path.GetFullPath(Path.Combine(options.Runtime.WorkspaceRoot, path));
    }
}
