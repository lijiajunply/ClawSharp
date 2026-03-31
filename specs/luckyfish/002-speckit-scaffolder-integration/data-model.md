# 数据模型：SpecKit 脚手架集成

## 1. SpecKit 配置模型

### SpecKitDefinition
表示一个 SpecKit 治理包。
- `Templates`: `IReadOnlyList<ProjectFileTemplate>` —— `.specify/templates/` 下的文件。
- `Scripts`: `IReadOnlyList<ProjectFileTemplate>` —— `.specify/scripts/` 下的脚本。
- `Memory`: `IReadOnlyList<ProjectFileTemplate>` —— `.specify/memory/` 下的基础文件（如 constitution.md）。

## 2. 功能开发模型

### FeatureMetadata
从 `spec.md` 或 `plan.md` 提取的元数据。
- `FeatureId`: `string` —— 如 `002`。
- `ShortName`: `string` —— 如 `speckit-integration`。
- `BranchName`: `string` —— 如 `luckyfish/002-speckit-integration`。
- `Status`: `string` —— `Draft`, `Approved`, `Implementing`, `Completed`。

### ScaffoldPlan
由 `plan.md` 驱动生成的动作计划。
- `GitBranchToCreate`: `string?` —— 建议的分支名称。
- `DirectoriesToCreate`: `IReadOnlyList<string>` —— 建议创建的目录。
- `FilesToScaffold`: `IReadOnlyList<ScaffoldFile>` —— 建议创建的占位文件。
- `TasksToGenerate`: `IReadOnlyList<string>` —— 建议注入 `tasks.md` 的内容。

### ScaffoldFile
- `Path`: `string` —— 文件的相对路径。
- `Kind`: `string` —— 文件类型（如 `CSharpClass`, `Markdown`, `Directory`）。
- `InitialContent`: `string?` —— AI 生成的基础代码。

## 3. 运行时状态模型

### FeatureContext
保存在 SQLite 中的功能当前状态，用于跟踪闭环进度。
- `FeatureId`: 主键。
- `CurrentPhase`: `Spec`, `Plan`, `Tasks`, `Implementation`。
- `IsScaffolded`: `bool` —— 是否已完成物理脚手架创建。
- `PlanChecksum`: `string` —— `plan.md` 的校验和，用于检测显著更新。
