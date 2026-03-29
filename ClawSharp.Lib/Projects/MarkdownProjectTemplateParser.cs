using ClawSharp.Lib.Agents;
using ClawSharp.Lib.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ClawSharp.Lib.Projects;

/// <summary>
/// 将模板目录中的元数据与文件解析为 <see cref="ProjectTemplateDefinition"/>。
/// </summary>
public sealed class MarkdownProjectTemplateParser
{
    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// 从模板目录解析项目模板定义。
    /// </summary>
    /// <param name="templateDirectory">模板目录绝对路径。</param>
    /// <returns>解析后的模板定义。</returns>
    public ProjectTemplateDefinition ParseDirectory(string templateDirectory)
    {
        if (!Directory.Exists(templateDirectory))
        {
            throw new ValidationException($"Project template directory '{templateDirectory}' does not exist.");
        }

        var markdownMetadataPath = Path.Combine(templateDirectory, "template.md");
        var yamlMetadataPath = Path.Combine(templateDirectory, "template.yaml");

        ProjectTemplateMetadata metadata;
        string readmeAppendix;

        if (File.Exists(markdownMetadataPath))
        {
            var markdown = File.ReadAllText(markdownMetadataPath);
            var (frontMatter, body) = MarkdownFrontMatter.Parse(markdown);
            metadata = _deserializer.Deserialize<ProjectTemplateMetadata>(frontMatter)
                       ?? throw new ValidationException($"Project template metadata in '{markdownMetadataPath}' is invalid.");
            readmeAppendix = body.Trim();
        }
        else if (File.Exists(yamlMetadataPath))
        {
            metadata = _deserializer.Deserialize<ProjectTemplateMetadata>(File.ReadAllText(yamlMetadataPath))
                       ?? throw new ValidationException($"Project template metadata in '{yamlMetadataPath}' is invalid.");
            var readmeAppendixPath = Path.Combine(templateDirectory, "README.append.md");
            readmeAppendix = File.Exists(readmeAppendixPath) ? File.ReadAllText(readmeAppendixPath).Trim() : string.Empty;
        }
        else
        {
            throw new ValidationException($"Project template directory '{templateDirectory}' must contain template.md or template.yaml.");
        }

        var files = Directory.EnumerateFiles(templateDirectory, "*", SearchOption.AllDirectories)
            .Where(path => !string.Equals(path, markdownMetadataPath, StringComparison.OrdinalIgnoreCase))
            .Where(path => !string.Equals(path, yamlMetadataPath, StringComparison.OrdinalIgnoreCase))
            .Where(path => !string.Equals(Path.GetFileName(path), "README.append.md", StringComparison.OrdinalIgnoreCase))
            .Select(path =>
            {
                var relativePath = Path.GetRelativePath(templateDirectory, path);
                return new ProjectFileTemplate(relativePath, File.ReadAllText(path));
            })
            .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var definition = new ProjectTemplateDefinition(
            metadata.Id ?? string.Empty,
            metadata.Name ?? string.Empty,
            metadata.Description ?? string.Empty,
            metadata.Version ?? string.Empty,
            (metadata.Directories ?? []).Select(path => new ProjectDirectoryTemplate(path)).ToArray(),
            files,
            metadata.Variables ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            readmeAppendix);

        definition.Validate();
        return definition;
    }

    private sealed class ProjectTemplateMetadata
    {
        public string? Id { get; init; }
        public string? Name { get; init; }
        public string? Description { get; init; }
        public string? Version { get; init; }
        public List<string>? Directories { get; init; }
        public Dictionary<string, string>? Variables { get; init; }
    }
}
