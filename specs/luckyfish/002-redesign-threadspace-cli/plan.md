# Implementation Plan: ThreadSpace 重新设计与 CLI 体验优化

**Branch**: `luckyfish/002-redesign-threadspace-cli` | **Date**: 2026-03-30 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/luckyfish/002-redesign-threadspace-cli/spec.md`

## Summary

将 ThreadSpace 数据模型中的 `BoundFolderPath` 改为可选字段，新增 `IsGlobal` 标志以区分全局与目录绑定的 ThreadSpace；同步更新 EF Core 迁移脚本。在 CLI 层面，将默认入口改为无参数 REPL，并参照 Gemini/Claude CLI 风格重写 `ChatCommand`，支持 `/help`、`/new`、`/resume`、`/cd`、`/home` 等斜杠命令和欢迎头部。

## Technical Context

**Language/Version**: C# 14 / .NET 10
**Primary Dependencies**: System.CommandLine, Spectre.Console, EF Core 9 + SQLite
**Storage**: SQLite via EF Core migrations (`ClawDbContext`)
**Testing**: xUnit, `ClawSharp.Lib.Tests` (integration tests using temp dir + full DI)
**Target Platform**: macOS / Linux / Windows (cross-platform CLI)
**Project Type**: CLI + Library
**Performance Goals**: REPL 冷启动 < 3 秒（SC-002）
**Constraints**: 向后兼容 `claw chat` 子命令（FR-012）；不破坏现有 `spaces`/`list`/`history` 命令
**Scale/Scope**: 单用户本地工具，无并发压力

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| 原则 | 检查结果 | 说明 |
|------|----------|------|
| I. Library-First | ✅ 通过 | ThreadSpace 模型变更在 `ClawSharp.Lib` 实现；CLI 仅调用 Lib 接口 |
| II. Markdown-Driven | ✅ 通过 | 本次无 Agent/Skill 定义变更 |
| III. Async-First & .NET 10 | ✅ 通过 | 所有新增操作均用 `async Task` |
| IV. Provider & Tool Extensibility | ✅ 通过 | 不影响 Provider 或 Tool 抽象 |
| V. Reliable Persistence | ✅ 通过 | 数据模型变更通过新 EF Core migration 实现 |

**POST-DESIGN RE-CHECK**: 无新增违规。`BoundFolderPath` nullable 的 unique index 在 SQLite 中允许多行 NULL，全局 ThreadSpace 是唯一 NULL 行，约束不受影响。

## Project Structure

### Documentation (this feature)

```text
specs/luckyfish/002-redesign-threadspace-cli/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── repl-slash-commands.md
└── tasks.md             # Phase 2 output (/speckit.tasks)
```

### Source Code (affected files)

```text
ClawSharp.Lib/
├── Runtime/
│   ├── ThreadSpaceContracts.cs            # BoundFolderPath nullable, IsInit→IsGlobal
│   ├── SqliteStores.cs (ThreadSpaceManager) # EnsureDefaultAsync 创建无路径全局TS
│   └── Persistence/
│       ├── Entities/ThreadSpaceEntity.cs  # BoundFolderPath nullable, IsInit→IsGlobal
│       ├── Configurations/ThreadSpaceEntityConfiguration.cs  # nullable mapping
│       ├── Migrations/
│       │   ├── 20260330010000_AddGlobalThreadSpace.cs  # NEW migration
│       │   └── ClawDbContextModelSnapshot.cs           # 更新 snapshot
│       └── Mapping/RuntimeEntityMapper.cs             # 映射更新
ClawSharp.CLI/
├── Program.cs                             # root command 默认处理器
└── Commands/ChatCommand.cs                # 完整重写 REPL

ClawSharp.Lib.Tests/
└── ThreadSpaceManagerTests.cs            # 新增全局TS测试
```

## Complexity Tracking

无 Constitution 违规，跳过此节。
