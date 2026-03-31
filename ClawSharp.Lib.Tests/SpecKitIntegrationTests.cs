using System.CommandLine;
using ClawSharp.CLI.Commands;
using ClawSharp.Lib.Agents;
using ClawSharp.Lib.Projects;
using ClawSharp.Lib.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace ClawSharp.Lib.Tests;

public sealed class SpecKitIntegrationTests : SpecKitIntegrationTestBase
{
    [Fact]
    public async Task SpecKitProvider_AppliesEmbeddedStructureToProject()
    {
        using var host = CreateHost();
        var provider = host.Services.GetRequiredService<ISpecKitProvider>();
        var projectRoot = Path.Combine(Root, "generated", "demo-project");
        Directory.CreateDirectory(projectRoot);

        var result = await provider.ApplyAsync(projectRoot);

        Assert.True(result.IsSuccess, result.Error);
        Assert.True(File.Exists(Path.Combine(projectRoot, ".specify", "templates", "spec-template.md")));
        Assert.True(File.Exists(Path.Combine(projectRoot, ".specify", "memory", "constitution.md")));
    }

    [Fact]
    public async Task ScaffoldAnalyzer_ParsesBranchFilesAndMilestones()
    {
        using var host = CreateHost();
        var analyzer = host.Services.GetRequiredService<IScaffoldAnalyzer>();
        WritePlan(CreateSamplePlan());

        var result = await analyzer.AnalyzePlanAsync(PlanPath);

        Assert.True(result.IsSuccess, result.Error);
        Assert.NotNull(result.Value);
        Assert.Equal("luckyfish/002-demo-feature", result.Value!.GitBranchToCreate);
        Assert.Contains(result.Value.FilesToScaffold, file => file.Path.EndsWith("DemoService.cs", StringComparison.Ordinal));
        Assert.Contains("Add DemoService implementation", result.Value.TasksToGenerate);
    }

    [Fact]
    public async Task PlannerAgent_CreatesBranchFilesTasksAndFeatureContext()
    {
        await InitializeGitRepositoryAsync();
        WritePlan(CreateSamplePlan());

        using var host = CreateHost();
        var analyzer = host.Services.GetRequiredService<IScaffoldAnalyzer>();
        var planner = host.Services.GetRequiredService<IPlannerAgent>();
        var contexts = host.Services.GetRequiredService<IFeatureContextRepository>();

        var analysis = await analyzer.AnalyzePlanAsync(PlanPath);
        Assert.True(analysis.IsSuccess, analysis.Error);

        var result = await planner.ExecuteScaffoldAsync(analysis.Value!);

        Assert.True(result.IsSuccess, result.Error);
        Assert.True(File.Exists(Path.Combine(Root, "ClawSharp.Lib", "Services", "DemoService.cs")));
        Assert.True(File.Exists(Path.Combine(Root, "docs", "feature-overview.md")));
        Assert.Contains("Planner Generated Tasks", await File.ReadAllTextAsync(TasksPath));

        var context = await contexts.GetAsync("002");
        Assert.NotNull(context);
        Assert.True(context!.IsScaffolded);
        Assert.Equal("Implementation", context.CurrentPhase);
    }

    [Fact]
    public async Task SpecKitScaffoldCommand_GeneratesFilesWithoutPromptWhenYesPassed()
    {
        await InitializeGitRepositoryAsync();
        WritePlan(CreateSamplePlan());

        using var host = CreateHost();
        var command = SpecKitCommands.Create(host);

        var exitCode = await command.InvokeAsync(["scaffold", "--yes", PlanPath]);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(Path.Combine(Root, "ClawSharp.Lib", "Services", "DemoService.cs")));
        Assert.True(File.Exists(TasksPath));
    }
}
