# 实施方案：扩展 AI 工具生态系统

**分支**: `luckyfish/002-extend-tool-ecosystem` | **日期**: 2026-03-30 | **规范**: [/specs/luckyfish/002-extend-tool-ecosystem/spec.md](spec.md)

## 摘要

本方案旨在扩展 ClawSharp 的内置工具集，使其能够处理网页内容（支持 JS 渲染）、CSV 结构化数据、Git 版本控制操作以及 PDF 文本提取。所有工具将作为 `ClawSharp.Lib` 的内置 `IToolExecutor` 实现，确保本地优先、性能卓越且与现有的权限系统深度集成。

## 技术上下文

**语言/版本**: C# 14 / .NET 10  
**主要依赖**: 
- `Microsoft.Playwright` (用于 Web 浏览器工具) [NEEDS CLARIFICATION: 是否有更轻量的 Playwright 替代方案，或如何最小化驱动依赖？]
- `CsvHelper` (用于 CSV 解析)
- `PdfPig` (用于 PDF 解析)
- `LibGit2Sharp` 或直接包装 `git` CLI [NEEDS CLARIFICATION: 考虑到本地优先和易用性，是使用 C# 绑定库还是直接调用系统 git 命令？]
**存储**: 无持续性存储需求，工具结果直接返回给 AI 或在 Session 中临时存在。
**测试**: xUnit (单元测试与 `ClawSharp.Lib.Tests` 中的集成测试)。
**目标平台**: Cross-platform (Windows, macOS, Linux)。
**项目类型**: 类库 (ClawSharp.Lib) 核心功能扩展。
**性能目标**: 工具启动与执行开销（不含 IO）控制在 500ms 内。
**约束**: 
- 必须支持 `ToolPermissionSet`。
- 分页处理大文件（CSV/PDF）。
- 网页获取需支持 JS 渲染。

## 章程检查

*门禁：在 Phase 0 研究前必须通过。Phase 1 设计后重新检查。*

| 准则 | 状态 | 备注 |
| :--- | :--- | :--- |
| **I. Library-First & Local-First** | ✅ 通过 | 所有工具均在 `ClawSharp.Lib` 中实现。 |
| **II. Markdown-Driven Intelligence** | ✅ 通过 | 工具描述支持元数据定义。 |
| **III. Async-First & Modern .NET** | ✅ 通过 | 所有工具执行必须为 `ExecuteAsync`。 |
| **IV. Provider & Tool Extensibility** | ✅ 通过 | 扩展内置工具集。 |
| **V. Reliable Persistence & Analytics** | ✅ 通过 | 工具调用会被 Session 系统记录。 |

## 项目结构

### 文档 (本功能)

```text
specs/luckyfish/002-extend-tool-ecosystem/
├── plan.md              # 本文件
├── research.md          # Phase 0 输出
├── data-model.md        # Phase 1 输出
├── quickstart.md        # Phase 1 输出
├── contracts/           # Phase 1 输出
└── tasks.md             # Phase 2 输出
```

### 源代码 (仓库根目录)

```text
ClawSharp.Lib/
└── Tools/
    ├── WebBrowserTool.cs  (新)
    ├── CsvReadTool.cs     (新)
    ├── GitOpsTools.cs     (新)
    └── PdfReadTool.cs     (新)

ClawSharp.Lib.Tests/
└── ToolExtensionTests.cs (新)
```

**结构决策**: 遵循现有 `ClawSharp.Lib/Tools` 目录结构，每个新工具类实现 `IToolExecutor` 接口。

## 复杂度跟踪

> **仅当章程检查存在必须辩护的违反项时填写**

| 违反项 | 必要性 | 被拒绝的更简单替代方案及原因 |
| :--- | :--- | :--- |
| 无 | N/A | N/A |
