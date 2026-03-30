# 任务列表：扩展 AI 工具生态系统

**输入**: 设计文档来自 `/specs/luckyfish/002-extend-tool-ecosystem/`
**前置条件**: plan.md (要求), spec.md (要求), research.md, data-model.md, contracts/

**组织方式**: 任务按用户场景（User Story）分组，以支持每个场景的独立实现和测试。

## 格式说明: `[ID] [P?] [Story] 描述`

- **[P]**: 可并行执行（不同文件，无未完成任务的依赖）
- **[Story]**: 任务所属的用户场景（如 US1, US2, US3）
- 描述中包含确切的文件路径

---

## 阶段 1: 设置 (共享基础设施)

**目的**: 项目初始化与依赖管理

- [ ] T001 在 `ClawSharp.Lib.csproj` 中添加 NuGet 依赖：`Microsoft.Playwright`, `CsvHelper`, `LibGit2Sharp`, `PdfPig`
- [ ] T002 创建 Playwright 初始化脚本或工具方法用于安装浏览器驱动

---

## 阶段 2: 基础建设 (阻塞性前置条件)

**目的**: 在实现具体用户场景前必须完成的核心架构工作

- [ ] T003 在 `ClawSharp.Lib/Tools/ToolContracts.cs` 中更新 `ToolCapability` 枚举，确保包含 `NetworkAccess` 并考虑新增 `VersionControl` 能力位
- [ ] T004 [P] 在 `ClawSharp.Lib/Tools/ToolContracts.cs` 中更新 `ToolCapabilityParser` 以支持新的能力位解析

**检查点**: 基础建设已就绪 - 现在可以并行开始用户场景的实现

---

## 阶段 3: 用户场景 1 - 网页研究与内容检索 (优先级: P1) 🎯 MVP

**目标**: 实现支持 JS 渲染的网页内容检索

**独立测试**: 使用 `web_browser` 工具访问包含异步加载内容的页面，验证返回的文本包含预期数据。

### 用户场景 1 实现

- [ ] T005 [P] [US1] 在 `ClawSharp.Lib.Tests/ToolExtensionTests.cs` 中编写 `WebBrowserTool` 的集成测试（初始应失败）
- [ ] T006 [US1] 在 `ClawSharp.Lib/Tools/WebBrowserTool.cs` 中实现 `WebBrowserTool` 类（基于 Playwright）
- [ ] T007 [US1] 在 `WebBrowserTool` 中添加 HTML 清理逻辑，仅返回对 AI 有用的纯文本内容
- [ ] T008 [US1] 实现 `WebBrowserTool` 的超时处理和 `MaxOutputLength` 截断逻辑

**检查点**: 此时用户场景 1 应能独立工作并通过测试

---

## 阶段 4: 用户场景 2 - 结构化数据分析 (优先级: P2)

**目标**: 实现 CSV 文件的结构化读取与分页

**独立测试**: 提供一个超过 100 行的 CSV，使用 `csv_read` 设置 `limit: 50` 和 `offset: 50`，验证返回的是第 51-100 行的数据。

### 用户场景 2 实现

- [ ] T009 [P] [US2] 在 `ClawSharp.Lib.Tests/ToolExtensionTests.cs` 中编写 `CsvReadTool` 的集成测试
- [ ] T010 [US2] 在 `ClawSharp.Lib/Tools/CsvReadTool.cs` 中实现 `CsvReadTool` 类（基于 CsvHelper）
- [ ] T011 [US2] 在 `CsvReadTool` 中实现分页逻辑（`limit` 和 `offset`）
- [ ] T012 [US2] 确保 `CsvReadTool` 遵守 `FileRead` 权限中的 `AllowedReadRoots` 限制

**检查点**: 用户场景 1 和 2 此时均应能独立工作

---

## 阶段 5: 用户场景 3 - 版本控制集成 (优先级: P3)

**目标**: 实现内置的 Git 操作工具

**独立测试**: 在测试仓库中运行 `git_ops` 的 `status` 操作，验证返回的 JSON 结构包含正确的文件状态。

### 用户场景 3 实现

- [ ] T013 [P] [US3] 在 `ClawSharp.Lib.Tests/ToolExtensionTests.cs` 中编写 `GitOpsTool` 的集成测试
- [ ] T014 [US3] 在 `ClawSharp.Lib/Tools/GitOpsTools.cs` 中实现 `GitOpsTool` 类（基于 LibGit2Sharp）
- [ ] T015 [US3] 实现 `status`, `log`, `diff` 三种核心操作的结构化输出
- [ ] T016 [US3] 确保 Git 操作仅限于 WorkspaceRoot 目录内

**检查点**: 前三个用户场景此时均应能独立工作

---

## 阶段 6: 用户场景 4 - 文档文本提取 (优先级: P3)

**目标**: 实现从 PDF 文件中提取纯文本

**独立测试**: 提供一个多页 PDF，使用 `pdf_read` 指定提取第 2 页，验证返回内容仅包含该页文本。

### 用户场景 4 实现

- [ ] T017 [P] [US4] 在 `ClawSharp.Lib.Tests/ToolExtensionTests.cs` 中编写 `PdfReadTool` 的集成测试
- [ ] T018 [US4] 在 `ClawSharp.Lib/Tools/PdfReadTool.cs` 中实现 `PdfReadTool` 类（基于 PdfPig）
- [ ] T019 [US4] 实现按页码提取的逻辑和异常处理（如加密 PDF）

**检查点**: 所有用户场景此时均应能独立工作

---

## 最终阶段: 磨合与横向关注点

**目的**: 确保所有工具正确注册并符合项目规范

- [ ] T020 [P] 在 `ClawSharp.Lib/Configuration/ServiceCollectionExtensions.cs` 中注册所有新工具执行器
- [ ] T021 执行 `quickstart.md` 中的所有验证案例
- [ ] T022 代码清理、文档更新及所有测试套件的最终运行

---

## 依赖关系与执行顺序

### 阶段依赖

- **设置 (阶段 1)**: 无依赖 - 可立即开始
- **基础建设 (阶段 2)**: 依赖设置完成 - 阻塞所有用户场景
- **用户场景 (阶段 3+)**: 均依赖基础建设阶段完成
  - 各个用户场景之间无强制依赖，可以按优先级顺序执行，也可以并行执行
- **磨合 (最终阶段)**: 依赖于所有选定的用户场景完成

### 并行机会

- 阶段 3-6 (US1, US2, US3, US4) 在基础建设完成后可以完全并行
- 每个场景内的测试编写 [P] 和部分文件创建 [P] 可以并行

---

## 并行示例：用户场景 1 & 2

```bash
# 同时开始 US1 和 US2 的测试编写
Task: "T005 [P] [US1] 编写 WebBrowserTool 的集成测试"
Task: "T009 [P] [US2] 编写 CsvReadTool 的集成测试"
```

---

## 实现策略

### MVP 优先 (仅用户场景 1)

1. 完成阶段 1 和 2。
2. 完成阶段 3 (网页浏览)。
3. **验证**: 独立测试网页浏览功能。
4. 交付/演示 MVP。

### 增量交付

1. 基础建设完成后，依次按 P1 -> P2 -> P3 顺序交付每个工具。
2. 每个工具完成后即进行集成测试，确保不破坏现有功能。

---

## 备注

- [P] 任务 = 不同文件，无依赖关系
- [Story] 标签将任务映射到具体的用户场景以实现可追溯性
- 每个任务的描述都必须足够具体，以便 AI 或开发者能够直接执行
