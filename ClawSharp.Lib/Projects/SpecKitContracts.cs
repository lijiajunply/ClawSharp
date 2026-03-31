using ClawSharp.Lib.Core;

namespace ClawSharp.Lib.Projects;

public sealed record SpecKitDefinition(
    IReadOnlyList<ProjectFileTemplate> Templates,
    IReadOnlyList<ProjectFileTemplate> Scripts,
    IReadOnlyList<ProjectFileTemplate> Memory);

public sealed record FeatureMetadata(
    string FeatureId,
    string ShortName,
    string BranchName,
    string Status,
    string FeatureRootPath,
    string PlanPath,
    string TasksPath);

public sealed record ScaffoldFile(
    string Path,
    string Kind,
    string? InitialContent);

public sealed record ScaffoldPlan(
    FeatureMetadata Metadata,
    string? GitBranchToCreate,
    IReadOnlyList<string> DirectoriesToCreate,
    IReadOnlyList<ScaffoldFile> FilesToScaffold,
    IReadOnlyList<string> TasksToGenerate);

public sealed record ApplySpecKitResult(
    string ProjectRootPath,
    IReadOnlyList<string> CreatedDirectories,
    IReadOnlyList<string> CreatedFiles);

public interface ISpecKitProvider
{
    Task<SpecKitDefinition> GetDefinitionAsync(CancellationToken cancellationToken = default);

    Task<OperationResult<ApplySpecKitResult>> ApplyAsync(
        string projectRoot,
        CancellationToken cancellationToken = default);
}

public interface IScaffoldAnalyzer
{
    Task<OperationResult<ScaffoldPlan>> AnalyzePlanAsync(string planPath, CancellationToken cancellationToken = default);
}
