# 核心接口定义：SpecKit 集成

## 1. ISpecKitProvider
负责提供标准 SpecKit 资源包。

```csharp
public interface ISpecKitProvider
{
    /// <summary>
    /// 加载当前全局 SpecKit 定义（模板、脚本等）。
    /// </summary>
    Task<SpecKitDefinition> GetDefinitionAsync(CancellationToken ct = default);

    /// <summary>
    /// 将 SpecKit 结构应用到指定的项目根目录。
    /// </summary>
    Task ApplyAsync(string projectRoot, CancellationToken ct = default);
}
```

## 2. IScaffoldAnalyzer
负责解析 `plan.md` 并生成动作建议。

```csharp
public interface IScaffoldAnalyzer
{
    /// <summary>
    /// 解析指定路径的规划文档，并返回动作建议。
    /// </summary>
    Task<OperationResult<ScaffoldPlan>> AnalyzePlanAsync(string planPath, CancellationToken ct = default);
}
```

## 3. IPlannerAgent
专门处理自举逻辑的 Agent 接口。

```csharp
public interface IPlannerAgent
{
    /// <summary>
    /// 执行脚手架生成动作（分支、文件、任务）。
    /// </summary>
    Task<OperationResult> ExecuteScaffoldAsync(ScaffoldPlan plan, CancellationToken ct = default);
}
```
