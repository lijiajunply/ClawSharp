# Research: ThreadSpace 重新设计与 CLI 体验优化

**Branch**: `luckyfish/002-redesign-threadspace-cli` | **Date**: 2026-03-30

---

## 决策 1: System.CommandLine 根命令默认 REPL 行为

**决策**: 在 `rootCommand` 上直接调用 `SetHandler(...)` 并把完整的 REPL 逻辑注入其中；同时保留 `chat` 子命令作为别名（用同一处理函数）。

**理由**: `System.CommandLine` 中，`RootCommand` 没有子命令被触发时，会执行其自身的 Handler。在 `Program.cs` 中对 `rootCommand` 设置与 `ChatCommand` 相同的 Handler 即可，无需破坏子命令路由。`chat` 子命令依然存在，调用相同的方法，满足 FR-012 向后兼容。

**替代方案**:
- 使用 `[default]` 命令标记（仅在某些框架中支持）：不适用于 System.CommandLine。
- 运行时检测参数为空时自动调用 chat 子命令：比直接设置 root handler 复杂，无优势。

---

## 决策 2: `IsInit` → `IsGlobal` 重命名 + `BoundFolderPath` 可选化

**决策**: 将 `ThreadSpaceEntity.IsInit` 重命名为 `IsGlobal`，对应数据库列从 `is_init` 改为 `is_global`；同时将 `BoundFolderPath` 从 `NOT NULL` 改为 `NULL`。通过新 EF Core migration `20260330010000_AddGlobalThreadSpace` 实现。

**理由**:
- 语义对齐规范（FR-004 明确要求 `IsGlobal` 标志）；
- EF Core migration 是本项目已有机制，保持一致；
- 初始 migration 已存在（`20260330000000_InitialRuntimePersistence`），新 migration 在其基础上追加。

**Migration 内容**:
```sql
-- SQLite 不支持 ALTER COLUMN，因此需要重建表
-- EF Core 的 SQLite provider 会生成重建表的 migration
ALTER TABLE thread_spaces RENAME TO thread_spaces_old;
CREATE TABLE thread_spaces (
    thread_space_id TEXT NOT NULL,
    name            TEXT NOT NULL,
    bound_folder_path TEXT NULL,   -- 改为 nullable
    is_global       INTEGER NOT NULL DEFAULT 0,  -- 原 is_init
    created_at      TEXT NOT NULL,
    archived_at     TEXT NULL,
    PRIMARY KEY (thread_space_id)
);
INSERT INTO thread_spaces SELECT thread_space_id, name, bound_folder_path, is_init, created_at, archived_at FROM thread_spaces_old;
DROP TABLE thread_spaces_old;
-- 重建索引（bound_folder_path 索引改为非唯一或改为条件唯一）
CREATE UNIQUE INDEX IX_thread_spaces_name ON thread_spaces (name);
CREATE UNIQUE INDEX IX_thread_spaces_bound_folder_path ON thread_spaces (bound_folder_path)
    WHERE bound_folder_path IS NOT NULL;
```

**替代方案**:
- 保留 `is_init` 列名，只在 C# 层改名：节省 migration 但导致 DB 列名与代码语义不一致，长期维护成本高，拒绝。
- 添加新列 `is_global` 而保留 `is_init`：产生冗余列，拒绝。

---

## 决策 3: 全局 ThreadSpace 的 `EnsureDefaultAsync` 策略

**决策**: `ThreadSpaceManager.EnsureDefaultAsync` 创建 global ThreadSpace 时，`BoundFolderPath = null`，`IsGlobal = true`，名称保持 `"init"`（内部保留名）。现有 `IsInit` 查找逻辑改为按 `IsGlobal = true` 查找。

**理由**: 现有代码用名称 `"init"` 作为保留标识符已行之有效，命名不变可避免已有数据库的记录失效。只需在逻辑层改为优先检查 `IsGlobal` 标志。全局 ThreadSpace 不绑定目录，其工具操作 `WorkspaceRoot` 使用 `ClawOptions.Runtime.WorkspaceRoot`。

**替代方案**:
- 用新名称如 `"__global__"`：破坏已有 init ThreadSpace 记录，需要额外迁移数据。

---

## 决策 4: `/resume` 最近会话加载策略

**决策**: 通过 `ISessionStore.ListByThreadSpaceAsync(threadSpaceId)` 获取当前 ThreadSpace 下所有 session（按 `StartedAt` 倒序），取第一条作为恢复目标。恢复时调用 `runtime.StartSessionAsync()` 替换为直接切换 `sessionId` 到已有 session（不创建新 session）。

**理由**: `ISessionStore.ListByThreadSpaceAsync` 已经按创建时间倒序返回（见 `EfSessionRecordRepository`）；`IClawRuntime.GetHistoryAsync(sessionId)` 可加载历史消息。REPL 只需把内部的 `sessionId` 变量指向已有 session 即可"恢复"，无需新 API。

**替代方案**:
- 向 `IClawRuntime` 增加 `ResumeSessionAsync`：为 thin CLI wrapper 增加 Lib 接口复杂度，不符合 Library-First 原则（能用现有 API 完成的不新增接口）。

---

## 决策 5: `ReplContext` 内部状态管理

**决策**: 在 `ChatCommand` 中用局部变量（`currentThreadSpaceId`、`currentSessionId`、`currentAgentId`、`currentThreadSpaceName`、`isGlobal`）追踪 REPL 上下文，不需要独立的 `ReplContext` 类。

**理由**: REPL 是单线程同步交互循环，局部变量已足够表达状态。引入独立类是过度设计（规范中 `ReplContext` 是概念实体，不要求一定是独立 class）。

**替代方案**:
- 提取 `ReplContext` class：如果将来需要异步多会话则有意义，当前不需要。

---

## 决策 6: 欢迎头部与提示符格式

**决策**:
- 欢迎头部：应用名 + 版本号 + 当前 Agent + 当前 ThreadSpace + 快捷命令提示
- 提示符：`[global] > ` 或 `[project-name] > `（与 ThemeConfig 统一）

**实现方式**: 从 `Assembly.GetEntryAssembly().GetName().Version` 读取版本；ThreadSpace 名称来自 `currentThreadSpace.Name`。对 global ThreadSpace 显示 `global`，对其他显示 `threadSpace.Name`。

**替代方案**:
- 使用 ASCII art banner：过于复杂，与 Gemini/Claude CLI 的简洁风格不符。

---

## 决策 7: EF Core Migration 生成策略

**决策**: 手写 migration 文件（`20260330010000_AddGlobalThreadSpace.cs`）而非使用 `dotnet ef migrations add`，因为：
1. 项目使用 .NET 10，工具链需匹配；
2. 手写迁移更精确控制 SQLite 表重建逻辑；
3. 同时更新 `ClawDbContextModelSnapshot.cs`。

**注意**: SQLite 不支持 `ALTER COLUMN`，EF Core 的 SQLite 会生成重建表的 migration（drop + create）。需要手动生成正确的重建 SQL。

---

## 所有 NEEDS CLARIFICATION 已解决

| 项目 | 状态 |
|------|------|
| System.CommandLine root handler 策略 | ✅ 已决策（root SetHandler + chat 别名） |
| BoundFolderPath nullable 迁移策略 | ✅ 已决策（新 migration，SQLite 表重建） |
| `/resume` 会话加载 API | ✅ 已决策（现有 ListByThreadSpaceAsync） |
| `IsInit` → `IsGlobal` 重命名范围 | ✅ 已决策（全链路：Entity + Record + Manager + Migration） |
| 欢迎头部内容 | ✅ 已决策（版本 + Agent + ThreadSpace） |
