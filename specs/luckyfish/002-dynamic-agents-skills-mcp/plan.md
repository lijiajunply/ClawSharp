# Implementation Plan: Dynamic Agents, Skills, and MCP Support

**Branch**: `luckyfish/002-dynamic-agents-skills-mcp` | **Date**: 2026-03-30 | **Spec**: [/specs/luckyfish/002-dynamic-agents-skills-mcp/spec.md]
**Input**: Feature specification from `/specs/luckyfish/002-dynamic-agents-skills-mcp/spec.md`

## Summary

本功能旨在增强 ClawSharp 的扩展性，支持从用户主目录（`~/.agent` 和 `~/.skills`）动态加载 Agent 和技能定义。同时，集成 MCP (Model Context Protocol) 1.0 规范，通过 `stdio` 传输协议连接外部工具服务器，从而极大地丰富内核的能力边界。

## Technical Context

**Language/Version**: C# 14 / .NET 10  
**Primary Dependencies**: `ClawSharp.Lib`, `Microsoft.Extensions.DependencyInjection`, `Spectre.Console`, `System.CommandLine`, `Microsoft.Extensions.Configuration`, `System.IO.FileSystem.Watcher` (用于动态刷新)  
**Storage**: 本地文件系统 (`~/.agent`, `~/.skills`, `~/.clawsharp/mcp.json`)，SQLite (会话持久化)  
**Testing**: xUnit (`ClawSharp.Lib.Tests`)  
**Target Platform**: 跨平台 (macOS, Linux, Windows)  
**Project Type**: Library (`ClawSharp.Lib`) & CLI (`ClawSharp.CLI`)  
**Performance Goals**: 启动时扫描耗时 < 500ms，动态刷新延迟 < 1s  
**Constraints**: Library-First (核心逻辑必须在 `ClawSharp.Lib`), Async-First, 严格遵循 Markdown + YAML 前置配置规范  
**Scale/Scope**: 支持至少 5 个并发 MCP 服务器进程

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] **Library-First**: 动态加载和 MCP 核心逻辑将实现在 `ClawSharp.Lib` 中，CLI 仅作为调用方。
- [x] **Markdown-Driven**: 动态 Agent 和技能完全遵循 Markdown + YAML 规范。
- [x] **Async-First**: 所有文件 IO 和 MCP 进程通信均采用异步模式。
- [x] **Modern .NET**: 使用 .NET 10 的最新 API（如新的文件系统 API 或进程管理 API）。
- [x] **Extensibility**: 通过 MCP 实现极致的工具扩展能力。

## Project Structure

### Documentation (this feature)

```text
specs/luckyfish/002-dynamic-agents-skills-mcp/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
└── tasks.md             # Phase 2 output (generated via /speckit.tasks)
```

### Source Code (repository root)

```text
ClawSharp.Lib/
├── Agents/              # 扩展 FileSystemAgentDefinitionStore 以支持多路径
├── Configuration/       # 添加 MCP 配置模型
├── Mcp/                 # [NEW] MCP 客户端协议实现
├── Skills/              # 扩展 FileSystemSkillDefinitionStore
└── Tools/               # 添加 McpToolProxy 以适配现有工具系统

ClawSharp.Lib.Tests/
├── McpTests.cs          # [NEW] MCP 协议单元测试
└── RegistryTests.cs     # 动态加载集成测试
```

**Structure Decision**: 遵循现有的 `ClawSharp.Lib` 模块化结构，并在 `Mcp` 目录下新增协议实现。

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

*(No violations identified)*
