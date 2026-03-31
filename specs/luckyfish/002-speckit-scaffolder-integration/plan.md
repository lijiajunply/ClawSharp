# Implementation Plan: SpecKit 脚手架集成与自举开发

**Branch**: `luckyfish/002-speckit-scaffolder-integration` | **Date**: 2026-03-31 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/luckyfish/002-speckit-scaffolder-integration/spec.md`

## Summary

本功能旨在将 SpecKit 治理框架深度集成到 ClawSharp 内核中。通过增强 `IProjectScaffolder` 实现“规范感知”的项目初始化，并开发专门的 Planner Agent，利用其解析 `plan.md` 的能力自动执行 Git 分支创建、占位文件生成和任务同步，实现开发流程的完全闭环。

## Technical Context

**Language/Version**: C# 14 / .NET 10  
**Primary Dependencies**: `Microsoft.Extensions.DependencyInjection`, `Spectre.Console`, `System.CommandLine`, `YamlDotNet`  
**Storage**: SQLite (用于会话和功能上下文持久化)  
**Testing**: xUnit (单元测试与集成测试)  
**Target Platform**: 跨平台 (Windows/macOS/Linux)
**Project Type**: Library (`ClawSharp.Lib`) 与 CLI (`ClawSharp.CLI`)  
**Performance Goals**: AI 处理脚手架生成时间 < 30s；初始化项目时间 < 2s。  
**Constraints**: 必须遵循 Async-First 异步优先和 Library-First 库优先原则。  
**Scale/Scope**: 覆盖整个 ClawSharp 开发生命周期（从初始化到功能交付）。

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] **I. Library-First**: 核心逻辑（`IProjectScaffolder` 增强, `PlannerAgent` 逻辑）必须实现在 `ClawSharp.Lib` 中。
- [x] **II. Markdown-Driven**: 所有 SpecKit 模板和生成的规划文档均使用 Markdown。
- [x] **III. Async-First**: 所有文件 IO、Git 操作和 AI 交互必须使用异步 `Task`。
- [x] **IV. Provider Extensibility**: Planner Agent 必须通过 `IModelProvider` 抽象与 LLM 交互。
- [x] **V. Reliable Persistence**: 功能上下文和自举进度应记录在 SQLite 中。

## Project Structure

### Documentation (this feature)

```text
specs/luckyfish/002-speckit-scaffolder-integration/
├── spec.md              # 功能规范
├── plan.md              # 本实施计划
├── research.md          # 技术研究与决策
├── data-model.md        # 数据模型定义
├── quickstart.md        # 快速入门指南
├── contracts/           # CLI 接口与 Agent 协议
└── tasks.md             # 任务跟踪 (由 /speckit.tasks 生成)
```

### Source Code (repository root)

```text
ClawSharp.Lib/
├── Projects/
│   ├── IProjectScaffolder.cs       # 接口更新
│   ├── ProjectScaffolder.cs        # 实现 SpecKit 嵌入
│   └── SpecKitDefinition.cs        # SpecKit 结构定义
├── Agents/
│   └── PlannerAgent.cs             # 新增：负责解析计划并执行脚手架
└── Runtime/
    └── FeatureContextRepository.cs  # 新增：遵循 Repository 模式管理功能元数据持久化

ClawSharp.CLI/
└── Commands/
    └── SpecKitCommands.cs          # 新增：/speckit 系列命令
```

**Structure Decision**: 采用 Option 1 (ClawSharp Core 聚焦)，将主要逻辑下沉到 `ClawSharp.Lib`，CLI 仅作为交互层。

## Complexity Tracking

*无宪章违反情况。*
