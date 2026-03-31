# 技术研究：SpecKit 脚手架集成与自举开发

## 1. 项目脚手架集成方案 (IProjectScaffolder)

### 现状分析
目前的 `ProjectScaffolder` 依赖于 `IProjectTemplateStore` 加载的 `ProjectTemplateDefinition`。这意味着如果要让每个项目都包含 SpecKit，需要修改所有的模板定义。

### 决策：装饰器或注入模式
**选择方案**：在 `ClawSharp.Lib` 中引入 `ISpecKitProvider` 接口。
- **理由**：通过解耦 SpecKit 的文件定义与具体的项目模板，可以实现全局一致的治理结构。
- **实施细节**：
    - `ISpecKitProvider` 负责提供 `.specify/` 目录下的标准模板和脚本。
    - 修改 `ProjectScaffolder`，在 `CreateProjectAsync` 的末尾调用 `ISpecKitProvider.ApplyAsync()`。

## 2. 计划解析与自举逻辑 (Plan Parsing)

### 解析目标
从 `plan.md` 中提取：
- 功能元数据（名称、状态、短名称）。
- 文件结构清单（用于创建占位文件）。
- 任务清单（用于生成 `tasks.md`）。

### 决策：基于正则表达式的区块解析
**选择方案**：开发 `MarkdownSectionParser` 类。
- **理由**：`plan.md` 结构相对固定，使用正则表达式提取特定的 Markdown 区块（如 `### Source Code`）是最轻量且高效的方法。

## 3. Planner Agent 交互模型

### 交互需求
用户保存 `plan.md` 后，系统需提示“检测到计划已更新，是否生成脚手架？”。

### 决策：CLI 层处理交互，Lib 层提供建议
- **ClawSharp.Lib**：提供 `IScaffoldAnalyzer`，用于对比当前磁盘状态与 `plan.md` 的差异，并返回一个 `ScaffoldPlan` 对象（包含建议创建的文件和分支）。
- **ClawSharp.CLI**：订阅文件系统变更或通过命令触发，接收 `ScaffoldPlan` 并使用 `Spectre.Console` 展示交互式确认。

## 4. Git 操作集成

### 技术选择
**选择方案**：直接调用系统 `git` 命令（通过已有的 `run_shell_command` 逻辑或 Lib 层的 Process 封装）。
- **理由**：维持“本地优先”原则，且 `git` 命令比 `LibGit2Sharp` 等库更易于调试和保持环境一致性。

## 5. 待澄清项解析

- **触发机制**：已确认为“交互式确认”。
- **文件覆盖**：脚手架生成应默认为“非破坏性”，即仅创建不存在的文件，不覆盖已有代码。
