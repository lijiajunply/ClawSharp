# 实施规划：CLI 交互体验升级 (REPL 2.0)

**功能分支**: `luckyfish/002-cli-repl-upgrade` | **日期**: 2026-03-31 | **规范**: [spec.md](spec.md)
**输入**: 来自 `/specs/luckyfish/002-cli-repl-upgrade/spec.md` 的功能规范。

## 摘要

升级 CLI REPL (`ChatCommand.cs`) 以提供更丰富的交互体验，包括会话管理、工具检查、多行输入（粘贴和编辑模式）以及带语法高亮的输出渲染。

## 技术上下文

**语言/版本**: C# 14 / .NET 10  
**主要依赖**: `ClawSharp.Lib`, `Spectre.Console`, `Microsoft.Extensions.DependencyInjection`  
**存储**: SQLite (通过 EF Core) 用于会话和消息历史。  
**测试**: xUnit (`ClawSharp.Lib.Tests`)  
**目标平台**: Windows, macOS, Linux (通过 .NET 10)  
**项目类型**: CLI 应用程序  
**性能目标**: 会话切换时间 < 5 秒，代码块实时语法高亮渲染。  
**约束**: 库优先 (核心逻辑位于 Lib), 异步优先。  
**规模/范围**: 对 `ClawSharp.CLI` REPL 环境的功能增强。

## 宪法检查

*闸门：必须在阶段 0 研究前通过。在阶段 1 设计后重新检查。*

| 原则 | 检查项 | 状态 |
| :--- | :--- | :--- |
| **I. 库优先 (Library-First)** | 会话管理和工具解析逻辑驻留在 `ClawSharp.Lib` 中。 | ✅ 通过 |
| **II. Markdown 驱动** | Agent/Skill 定义保持 Markdown 格式。 | ✅ 通过 |
| **III. 异步优先** | 所有运行时和持久化操作均为异步。 | ✅ 通过 |
| **IV. 提供商扩展性** | 兼容 `IModelProvider` 支持的所有模型提供商。 | ✅ 通过 |
| **V. 可靠持久化** | 会话和历史管理沿用现有的 EF Core/SQLite 架构。 | ✅ 通过 |

## 项目结构

### 文档 (本功能)

```text
specs/luckyfish/002-cli-repl-upgrade/
├── plan.md              # 本文件
├── research.md          # 阶段 0 输出
├── data-model.md        # 阶段 1 输出
├── quickstart.md        # 阶段 1 输出
├── checklists/          # 验证清单
└── spec.md              # 功能规范
```

### 源代码 (仓库根目录)

```text
ClawSharp.Lib/
├── Runtime/             # 会话和历史逻辑
└── Tools/               # 工具元数据逻辑

ClawSharp.CLI/
├── Commands/
│   └── ChatCommand.cs   # 主要 REPL 逻辑更新
└── Infrastructure/
    └── ReplPrompt.cs    # 多行输入和幽灵文本输入更新
```

**结构决策**: 实施将主要涉及 `ClawSharp.CLI` 以处理 REPL 交互逻辑，同时利用功能强大的 `ClawSharp.Lib` 获取会话和工具元数据。

## 复杂度跟踪

| 违规项 | 必要性说明 | 拒绝简单替代方案的原因 |
|-----------|------------|-------------------------------------|
| 无 | 不适用 | 不适用 |
