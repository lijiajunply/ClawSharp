using ClawSharp.Lib.Core;

namespace ClawSharp.Lib.Projects;

/// <summary>
/// SpecKit 资源定义，包含模板、脚本和记忆文件。
/// </summary>
/// <param name="Templates">要写入项目的模板文件。</param>
/// <param name="Scripts">随 SpecKit 一起提供的脚本文件。</param>
/// <param name="Memory">需要初始化的记忆文件。</param>
public sealed record SpecKitDefinition(
    IReadOnlyList<ProjectFileTemplate> Templates,
    IReadOnlyList<ProjectFileTemplate> Scripts,
    IReadOnlyList<ProjectFileTemplate> Memory);

/// <summary>
/// 功能脚手架相关的元数据。
/// </summary>
/// <param name="FeatureId">功能唯一标识。</param>
/// <param name="ShortName">功能短名称。</param>
/// <param name="BranchName">建议使用的 Git 分支名。</param>
/// <param name="Status">当前功能状态。</param>
/// <param name="FeatureRootPath">功能根目录路径。</param>
/// <param name="PlanPath">计划文件路径。</param>
/// <param name="TasksPath">任务文件路径。</param>
public sealed record FeatureMetadata(
    string FeatureId,
    string ShortName,
    string BranchName,
    string Status,
    string FeatureRootPath,
    string PlanPath,
    string TasksPath);

/// <summary>
/// 需要创建的脚手架文件描述。
/// </summary>
/// <param name="Path">文件相对路径。</param>
/// <param name="Kind">文件类型或用途标识。</param>
/// <param name="InitialContent">文件的初始内容。</param>
public sealed record ScaffoldFile(
    string Path,
    string Kind,
    string? InitialContent);

/// <summary>
/// 由计划分析器生成的完整脚手架执行计划。
/// </summary>
/// <param name="Metadata">功能元数据。</param>
/// <param name="GitBranchToCreate">需要创建或切换到的 Git 分支。</param>
/// <param name="DirectoriesToCreate">要创建的目录列表。</param>
/// <param name="FilesToScaffold">要生成的文件列表。</param>
/// <param name="TasksToGenerate">要写入任务文件的任务列表。</param>
public sealed record ScaffoldPlan(
    FeatureMetadata Metadata,
    string? GitBranchToCreate,
    IReadOnlyList<string> DirectoriesToCreate,
    IReadOnlyList<ScaffoldFile> FilesToScaffold,
    IReadOnlyList<string> TasksToGenerate);

/// <summary>
/// 将 SpecKit 应用到项目后的结果。
/// </summary>
/// <param name="ProjectRootPath">项目根目录路径。</param>
/// <param name="CreatedDirectories">已创建目录列表。</param>
/// <param name="CreatedFiles">已创建文件列表。</param>
public sealed record ApplySpecKitResult(
    string ProjectRootPath,
    IReadOnlyList<string> CreatedDirectories,
    IReadOnlyList<string> CreatedFiles);

/// <summary>
/// 提供 SpecKit 定义并将其应用到项目目录的服务。
/// </summary>
public interface ISpecKitProvider
{
    /// <summary>
    /// 获取当前可用的 SpecKit 定义。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>SpecKit 定义。</returns>
    Task<SpecKitDefinition> GetDefinitionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 将 SpecKit 内容应用到指定项目目录。
    /// </summary>
    /// <param name="projectRoot">项目根目录。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>应用结果。</returns>
    Task<OperationResult<ApplySpecKitResult>> ApplyAsync(
        string projectRoot,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 分析计划文件并生成脚手架执行计划的服务。
/// </summary>
public interface IScaffoldAnalyzer
{
    /// <summary>
    /// 解析计划文件并生成脚手架计划。
    /// </summary>
    /// <param name="planPath">计划文件路径。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>分析结果及脚手架计划。</returns>
    Task<OperationResult<ScaffoldPlan>> AnalyzePlanAsync(string planPath, CancellationToken cancellationToken = default);
}
