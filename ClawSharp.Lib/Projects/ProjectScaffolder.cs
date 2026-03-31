using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Core;

namespace ClawSharp.Lib.Projects;

/// <summary>
/// 默认的项目脚手架服务实现。
/// </summary>
public sealed class ProjectScaffolder(
    IProjectTemplateStore templateStore,
    ClawOptions options,
    ISpecKitProvider specKitProvider) : IProjectScaffolder
{
    /// <inheritdoc />
    public Task<IReadOnlyList<ProjectTemplateDefinition>> ListTemplatesAsync(
        CancellationToken cancellationToken = default) =>
        templateStore.LoadAllAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<OperationResult<CreateProjectResult>> CreateProjectAsync(
        CreateProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        request.Validate();

        var templates = await templateStore.LoadAllAsync(cancellationToken).ConfigureAwait(false);
        var template = templates.SingleOrDefault(x =>
            string.Equals(x.Id, request.ProjectType, StringComparison.OrdinalIgnoreCase));
        if (template is null)
        {
            return OperationResult<CreateProjectResult>.Failure(
                $"Project template '{request.ProjectType}' was not found.");
        }

        try
        {
            var projectRoot = ResolveProjectRoot(request.TargetPath, options.Runtime.WorkspaceRoot);
            if (request.OverwriteMode == OverwriteMode.RejectIfExists && Directory.Exists(projectRoot))
            {
                return OperationResult<CreateProjectResult>.Failure(
                    $"Project directory '{projectRoot}' already exists.");
            }

            var variables = BuildVariables(request, template);
            var createdDirectories = new List<string>();
            var createdFiles = new List<string>();

            Directory.CreateDirectory(projectRoot);
            createdDirectories.Add(projectRoot);

            foreach (var directoryTemplate in template.Directories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = ProjectTemplateRenderer.Render(directoryTemplate.RelativePath, variables);
                var fullPath = ResolveTemplateOutputPath(projectRoot, relativePath);
                if (!Directory.Exists(fullPath))
                {
                    Directory.CreateDirectory(fullPath);
                    createdDirectories.Add(fullPath);
                }
            }

            foreach (var fileTemplate in template.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = ProjectTemplateRenderer.Render(fileTemplate.RelativePath, variables);
                var fullPath = ResolveTemplateOutputPath(projectRoot, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                if (!createdDirectories.Contains(Path.GetDirectoryName(fullPath)!, StringComparer.OrdinalIgnoreCase))
                {
                    createdDirectories.Add(Path.GetDirectoryName(fullPath)!);
                }

                var content = ProjectTemplateRenderer.Render(fileTemplate.Content, variables);
                await File.WriteAllTextAsync(fullPath, content, cancellationToken).ConfigureAwait(false);
                createdFiles.Add(fullPath);
            }

            var readmePath = Path.Combine(projectRoot, "README.md");
            var readmeContent = BuildReadme(template, variables);
            await File.WriteAllTextAsync(readmePath, readmeContent, cancellationToken).ConfigureAwait(false);
            createdFiles.Add(readmePath);

            var specKitResult = await ApplySpecKitAsync(projectRoot, cancellationToken).ConfigureAwait(false);
            if (!specKitResult.IsSuccess)
            {
                return OperationResult<CreateProjectResult>.Failure(specKitResult.Error ?? "Failed to apply SpecKit.");
            }

            createdDirectories.AddRange(specKitResult.Value?.CreatedDirectories ?? []);
            createdFiles.AddRange(specKitResult.Value?.CreatedFiles ?? []);

            return OperationResult<CreateProjectResult>.Success(
                new CreateProjectResult(
                    template.Id,
                    request.ProjectName,
                    projectRoot,
                    createdDirectories.Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
                    createdFiles.Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray()));
        }
        catch (ValidationException ex)
        {
            return OperationResult<CreateProjectResult>.Failure(ex.Message);
        }
    }

    public Task<OperationResult<ApplySpecKitResult>> ApplySpecKitAsync(
        string projectRoot,
        CancellationToken cancellationToken = default) =>
        specKitProvider.ApplyAsync(projectRoot, cancellationToken);

    private static IReadOnlyDictionary<string, string> BuildVariables(CreateProjectRequest request,
        ProjectTemplateDefinition template)
    {
        var variables = new Dictionary<string, string>(template.DefaultVariables, StringComparer.OrdinalIgnoreCase)
        {
            ["project_name"] = request.ProjectName,
            ["project_type"] = template.Id,
            ["created_at"] = DateTimeOffset.UtcNow.ToString("O")
        };

        foreach (var pair in request.Variables ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
        {
            variables[pair.Key] = pair.Value;
        }

        if (!variables.TryGetValue("project_summary", out var summary) || string.IsNullOrWhiteSpace(summary))
        {
            variables["project_summary"] = template.Description;
        }

        return variables;
    }

    private static string BuildReadme(ProjectTemplateDefinition template, IReadOnlyDictionary<string, string> variables)
    {
        var lines = new List<string>
        {
            $"# {variables["project_name"]}",
            string.Empty,
            $"项目类型：`{variables["project_type"]}`",
            string.Empty,
            "## 项目说明",
            ProjectTemplateRenderer.Render(variables["project_summary"], variables),
            string.Empty,
            "## 创建信息",
            $"- 创建时间：`{variables["created_at"]}`"
        };

        if (!string.IsNullOrWhiteSpace(template.ReadmeAppendix))
        {
            lines.Add(string.Empty);
            lines.Add(ProjectTemplateRenderer.Render(template.ReadmeAppendix.Trim(), variables));
        }

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static string ResolveProjectRoot(string targetPath, string workspaceRoot)
    {
        var fullPath =
            Path.GetFullPath(Path.IsPathRooted(targetPath) ? targetPath : Path.Combine(workspaceRoot, targetPath));
        return fullPath;
    }

    private static string ResolveTemplateOutputPath(string projectRoot, string relativePath)
    {
        ProjectTemplateDefinition.ValidateRelativePath(relativePath, allowFileName: true);
        var fullPath = Path.GetFullPath(Path.Combine(projectRoot, relativePath));
        var normalizedRoot = EnsureTrailingSeparator(Path.GetFullPath(projectRoot));
        var normalizedPath = Path.GetFullPath(fullPath);
        return !normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
            ? throw new ValidationException($"Rendered template path '{relativePath}' escapes project root.")
            : normalizedPath;
    }

    private static string EnsureTrailingSeparator(string path)
    {
        if (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar))
        {
            return path;
        }

        return path + Path.DirectorySeparatorChar;
    }
}
