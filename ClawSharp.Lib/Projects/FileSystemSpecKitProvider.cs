using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Core;

namespace ClawSharp.Lib.Projects;

internal sealed class FileSystemSpecKitProvider(ClawOptions options) : ISpecKitProvider
{
    private readonly string _specKitRoot = ResolveSpecKitRoot(options);

    public async Task<SpecKitDefinition> GetDefinitionAsync(CancellationToken cancellationToken = default)
    {
        var templates = await LoadGroupAsync("templates", cancellationToken).ConfigureAwait(false);
        var scripts = await LoadGroupAsync("scripts", cancellationToken).ConfigureAwait(false);
        var memory = await LoadGroupAsync("memory", cancellationToken).ConfigureAwait(false);
        return new SpecKitDefinition(templates, scripts, memory);
    }

    public async Task<OperationResult<ApplySpecKitResult>> ApplyAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Directory.Exists(_specKitRoot))
            {
                return OperationResult<ApplySpecKitResult>.Failure(
                    $"SpecKit root '{_specKitRoot}' does not exist.");
            }

            var definition = await GetDefinitionAsync(cancellationToken).ConfigureAwait(false);
            var createdDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var createdFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in new[] { definition.Templates, definition.Scripts, definition.Memory })
            {
                foreach (var file in group)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var fullPath = Path.Combine(projectRoot, file.RelativePath);
                    var directory = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                        createdDirectories.Add(directory);
                    }

                    if (File.Exists(fullPath))
                    {
                        continue;
                    }

                    await File.WriteAllTextAsync(fullPath, file.Content, cancellationToken).ConfigureAwait(false);
                    createdFiles.Add(fullPath);
                }
            }

            return OperationResult<ApplySpecKitResult>.Success(
                new ApplySpecKitResult(
                    projectRoot,
                    createdDirectories.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
                    createdFiles.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray()));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ValidationException)
        {
            return OperationResult<ApplySpecKitResult>.Failure(ex.Message);
        }
    }

    private async Task<IReadOnlyList<ProjectFileTemplate>> LoadGroupAsync(
        string groupName,
        CancellationToken cancellationToken)
    {
        var groupRoot = Path.Combine(_specKitRoot, groupName);
        if (!Directory.Exists(groupRoot))
        {
            return [];
        }

        var files = new List<ProjectFileTemplate>();
        foreach (var file in Directory.EnumerateFiles(groupRoot, "*", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = Path.GetRelativePath(_specKitRoot, file);
            var content = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
            files.Add(new ProjectFileTemplate(Path.Combine(".specify", relativePath), content));
        }

        return files;
    }

    private static string ResolveSpecKitRoot(ClawOptions options)
    {
        var path = options.Projects.SpecKitPath;
        return Path.GetFullPath(Path.IsPathRooted(path)
            ? path
            : Path.Combine(options.Runtime.WorkspaceRoot, path));
    }
}
