# Implementation Plan: DuckDB 分析层落地与 `claw stats` 命令

**Branch**: `luckyfish/002-claw-stats-duckdb` | **Date**: 2026-03-31 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/luckyfish/002-claw-stats-duckdb/spec.md`

## Summary
本项目旨在通过扩展 `ISessionAnalyticsService` 实现对 DuckDB 分析数据的深层挖掘，并在 `ClawSharp.CLI` 中增加 `claw stats` 命令，以便用户直观地查看 Token 消耗趋势、工具调用频率和 Agent 性能指标。

## Technical Context

**Language/Version**: C# 14 / .NET 10  
**Primary Dependencies**: `ClawSharp.Lib`, `Spectre.Console`, `DuckDB.NET`  
**Storage**: DuckDB (分析存储), SQLite (操作存储)  
**Testing**: xUnit (`ClawSharp.Lib.Tests`)  
**Target Platform**: macOS/Windows/Linux (CLI)
**Project Type**: CLI Application + Library extension  
**Performance Goals**: 统计查询响应时间 < 2s  
**Constraints**: 必须符合 Library-First 原则，逻辑保留在核心库。

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] **原则 I (Library-First)**: 所有分析逻辑均在 `ClawSharp.Lib` 中实现。
- [x] **原则 III (Async-First)**: 所有 DuckDB 查询均采用异步模式。
- [x] **原则 V (Reliable Persistence & Analytics)**: 使用 DuckDB 进行高效本地分析。

## Project Structure

### Documentation (this feature)

```text
specs/luckyfish/002-claw-stats-duckdb/
├── plan.md              # 本计划文件
├── research.md          # 研究报告：DuckDB 表结构与 JSON 提取逻辑
├── data-model.md        # 分析实体定义
├── quickstart.md        # 命令使用指南
├── contracts/           
│   └── AnalyticsContracts.md  # 接口契约定义
└── tasks.md             # (由 /speckit.tasks 生成)
```

### Source Code (repository root)

```text
ClawSharp.Lib/
├── Runtime/
│   ├── AnalyticsContracts.cs   # 扩展 ISessionAnalyticsService 接口
│   └── AnalyticsServices.cs    # 实现 DuckDB 查询逻辑

ClawSharp.CLI/
├── Commands/
│   └── StatsCommands.cs        # 实现 claw stats 命令
├── Infrastructure/
│   └── StatsRenderer.cs        # 使用 Spectre.Console 渲染统计数据

ClawSharp.Lib.Tests/
└── AnalyticsTests.cs           # 增加集成测试验证统计逻辑
```

**Structure Decision**: 采用标准的 ClawSharp 架构模式。核心分析逻辑位于 `ClawSharp.Lib` 的 `Runtime` 空间，CLI 仅作为交互层。

## Phase 0: Research (Completed)
- 研究了 `IDuckDbAnalyticsProjector` 的现有表结构。
- 确定了通过 `json_extract` 提取 `Usage` 和耗时数据的 SQL 路径。
- 确认了 `TurnCompleted` 事件是获取 Token 和延迟信息的主要来源。

## Phase 1: Design & Contracts (Completed)
- 定义了 `ISessionAnalyticsService` 的扩展方法。
- 定义了分析结果实体（Token、Tool、Agent）。
- 设计了 CLI 命令的参数和输出格式。
- 更新了 Agent 上下文。

## Phase 2: Implementation (Pending)
- 待执行 `/speckit.tasks` 生成具体任务清单。
