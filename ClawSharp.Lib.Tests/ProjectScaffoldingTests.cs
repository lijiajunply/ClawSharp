using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Core;
using ClawSharp.Lib.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace ClawSharp.Lib.Tests;

public sealed class ProjectScaffoldingTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "claw-project-tests", Guid.NewGuid().ToString("N"));

    public ProjectScaffoldingTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task TemplateStore_LoadsTemplatesFromDirectory()
    {
        CreatePaperTemplate();
        CreateReportTemplate();

        var options = new ClawOptions
        {
            Runtime = new RuntimeOptions { WorkspaceRoot = _root },
            Projects = new ProjectOptions { TemplatesPath = Path.Combine(_root, "workspace", "project-templates") }
        };

        var store = new FileSystemProjectTemplateStore(options, new MarkdownProjectTemplateParser());
        var templates = await store.LoadAllAsync();

        Assert.Equal(2, templates.Count);
        Assert.Contains(templates, x => x.Id == "paper");
        Assert.Contains(templates, x => x.Id == "report");
    }

    [Fact]
    public async Task TemplateStore_DuplicateIds_Throws()
    {
        CreateTemplate(
            "paper-one",
            """
---
id: paper
name: Paper One
description: First paper template
version: v1
---
""");
        CreateTemplate(
            "paper-two",
            """
---
id: paper
name: Paper Two
description: Second paper template
version: v1
---
""");

        var options = new ClawOptions
        {
            Runtime = new RuntimeOptions { WorkspaceRoot = _root },
            Projects = new ProjectOptions { TemplatesPath = Path.Combine(_root, "workspace", "project-templates") }
        };

        var store = new FileSystemProjectTemplateStore(options, new MarkdownProjectTemplateParser());
        await Assert.ThrowsAsync<ValidationException>(() => store.LoadAllAsync());
    }

    [Fact]
    public async Task TemplateStore_PathTraversal_Throws()
    {
        CreateTemplate(
            "bad",
            """
---
id: bad
name: Bad
description: Invalid
version: v1
directories:
  - ../escape
---
""");

        var options = new ClawOptions
        {
            Runtime = new RuntimeOptions { WorkspaceRoot = _root },
            Projects = new ProjectOptions { TemplatesPath = Path.Combine(_root, "workspace", "project-templates") }
        };

        var store = new FileSystemProjectTemplateStore(options, new MarkdownProjectTemplateParser());
        await Assert.ThrowsAsync<ValidationException>(() => store.LoadAllAsync());
    }

    [Fact]
    public async Task Scaffolder_CreatesPaperProjectWithReadme()
    {
        CreatePaperTemplate();

        var scaffolder = CreateScaffolder();
        var result = await scaffolder.CreateProjectAsync(
            new CreateProjectRequest(
                "paper",
                "Quantum Notes",
                "generated/paper-project",
                new Dictionary<string, string>
                {
                    ["author"] = "Lucky Fish",
                    ["project_summary"] = "用于整理量子计算论文和实验记录。"
                }));

        Assert.True(result.IsSuccess, result.Error);
        Assert.NotNull(result.Value);

        var projectRoot = Path.Combine(_root, "generated", "paper-project");
        Assert.True(Directory.Exists(projectRoot));
        Assert.Contains(Path.Combine(projectRoot, "docs"), result.Value!.CreatedDirectories);
        Assert.Contains(Path.Combine(projectRoot, "references"), result.Value.CreatedDirectories);
        Assert.Contains(Path.Combine(projectRoot, "docs", "outline.md"), result.Value.CreatedFiles);

        var outline = await File.ReadAllTextAsync(Path.Combine(projectRoot, "docs", "outline.md"));
        Assert.Contains("Quantum Notes", outline);
        Assert.Contains("Lucky Fish", outline);

        var readme = await File.ReadAllTextAsync(Path.Combine(projectRoot, "README.md"));
        Assert.Contains("# Quantum Notes", readme);
        Assert.Contains("项目类型：`paper`", readme);
        Assert.Contains("用于整理量子计算论文和实验记录。", readme);
        Assert.Contains("## 研究背景", readme);
        Assert.Contains(Path.Combine(projectRoot, ".specify", "templates", "spec-template.md"), result.Value.CreatedFiles);
    }

    [Fact]
    public async Task Scaffolder_CreatesReportProjectWithDifferentStructure()
    {
        CreateReportTemplate();

        var scaffolder = CreateScaffolder();
        var result = await scaffolder.CreateProjectAsync(
            new CreateProjectRequest(
                "report",
                "Weekly Sync",
                "generated/report-project",
                new Dictionary<string, string>
                {
                    ["audience"] = "产品团队"
                }));

        Assert.True(result.IsSuccess, result.Error);
        Assert.NotNull(result.Value);

        var projectRoot = Path.Combine(_root, "generated", "report-project");
        Assert.True(File.Exists(Path.Combine(projectRoot, "slides", "agenda.md")));
        Assert.True(File.Exists(Path.Combine(projectRoot, "notes", "summary.md")));

        var agenda = await File.ReadAllTextAsync(Path.Combine(projectRoot, "slides", "agenda.md"));
        Assert.Contains("Weekly Sync", agenda);
        Assert.Contains("产品团队", agenda);
    }

    [Fact]
    public async Task Scaffolder_CustomVariablesOverrideTemplateDefaults()
    {
        CreatePaperTemplate();

        var scaffolder = CreateScaffolder();
        var result = await scaffolder.CreateProjectAsync(
            new CreateProjectRequest(
                "paper",
                "Override Demo",
                "generated/override-project",
                new Dictionary<string, string>
                {
                    ["author"] = "Custom Author"
                }));

        Assert.True(result.IsSuccess, result.Error);

        var outline = await File.ReadAllTextAsync(Path.Combine(_root, "generated", "override-project", "docs", "outline.md"));
        Assert.Contains("Custom Author", outline);
        Assert.DoesNotContain("Unknown Author", outline);
    }

    [Fact]
    public async Task Scaffolder_RejectsExistingDirectory()
    {
        CreatePaperTemplate();
        var target = Path.Combine(_root, "generated", "existing-project");
        Directory.CreateDirectory(target);

        var scaffolder = CreateScaffolder();
        var result = await scaffolder.CreateProjectAsync(
            new CreateProjectRequest("paper", "Existing", "generated/existing-project"));

        Assert.False(result.IsSuccess);
        Assert.Contains("already exists", result.Error);
    }

    [Fact]
    public async Task AddClawSharp_RegistersProjectServicesAndResolvesRelativeTemplatesPath()
    {
        CreatePaperTemplate();

        var services = new ServiceCollection();
        services.AddClawSharp(builder => { builder.BasePath = _root; });

        await using var provider = services.BuildServiceProvider();
        var scaffolder = provider.GetRequiredService<IProjectScaffolder>();
        var templates = await scaffolder.ListTemplatesAsync();

        Assert.Single(templates);
        Assert.Equal("paper", templates[0].Id);
    }

    private IProjectScaffolder CreateScaffolder()
    {
        var options = new ClawOptions
        {
            Runtime = new RuntimeOptions { WorkspaceRoot = _root },
            Projects = new ProjectOptions { TemplatesPath = Path.Combine(_root, "workspace", "project-templates") }
        };

        var store = new FileSystemProjectTemplateStore(options, new MarkdownProjectTemplateParser());
        return new ProjectScaffolder(store, options, new FakeSpecKitProvider());
    }

    private void CreatePaperTemplate()
    {
        CreateTemplate(
            "paper",
            """
---
id: paper
name: Paper Project
description: 用于组织论文写作和资料整理的项目模板。
version: v1
directories:
  - docs
  - references
variables:
  author: Unknown Author
---
## 研究背景
记录研究问题、关键假设和参考资料。

## 写作计划
列出里程碑、投稿目标和实验安排。
""",
            ("docs/outline.md", "# {{project_name}}\n\n作者：{{author}}\n"),
            ("references/bibliography.md", "# References\n"),
            ("notes-{{project_name}}.md", "项目：{{project_name}}\n"));
    }

    private void CreateReportTemplate()
    {
        CreateTemplate(
            "report",
            """
---
id: report
name: Report Project
description: 用于准备汇报结构、讲稿和会后纪要的项目模板。
version: v1
directories:
  - slides
  - notes
variables:
  audience: Stakeholders
---
## 受众与目标
说明汇报受众、核心目标和预期决策。

## 交付清单
列出讲稿、附件与跟进事项。
""",
            ("slides/agenda.md", "# {{project_name}}\n\n面向：{{audience}}\n"),
            ("notes/summary.md", "会议纪要：{{project_name}}\n"));
    }

    private void CreateTemplate(string folderName, string templateMarkdown, params (string RelativePath, string Content)[] files)
    {
        var templateRoot = Path.Combine(_root, "workspace", "project-templates", folderName);
        Directory.CreateDirectory(templateRoot);
        File.WriteAllText(Path.Combine(templateRoot, "template.md"), templateMarkdown);

        foreach (var (relativePath, content) in files)
        {
            var fullPath = Path.Combine(templateRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class FakeSpecKitProvider : ISpecKitProvider
    {
        public Task<SpecKitDefinition> GetDefinitionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SpecKitDefinition(
                [new ProjectFileTemplate(Path.Combine(".specify", "templates", "spec-template.md"), "# Spec Template\n")],
                [],
                []));

        public Task<OperationResult<ApplySpecKitResult>> ApplyAsync(string projectRoot, CancellationToken cancellationToken = default)
        {
            var targetPath = Path.Combine(projectRoot, ".specify", "templates", "spec-template.md");
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.WriteAllText(targetPath, "# Spec Template\n");
            return Task.FromResult(OperationResult<ApplySpecKitResult>.Success(
                new ApplySpecKitResult(
                    projectRoot,
                    [Path.Combine(projectRoot, ".specify"), Path.Combine(projectRoot, ".specify", "templates")],
                    [targetPath])));
        }
    }
}
