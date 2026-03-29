# Quickstart: ThreadSpace 重新设计与 CLI 体验优化

**Branch**: `luckyfish/002-redesign-threadspace-cli` | **Date**: 2026-03-30

---

## 新行为概览

| 场景 | 旧行为 | 新行为 |
|------|--------|--------|
| 直接运行 `claw` | 显示帮助文档 | 直接进入 REPL |
| REPL 提示符 | 无明确标识 | `[global] > ` 或 `[project-name] > ` |
| 切换项目 | 需用 `/init` 命令 | `/cd /path/to/project` |
| 回到全局 | 不支持 | `/home` |
| 恢复上次对话 | 不支持 | `/resume` |
| 帮助文档 | 需退出查文档 | `/help` 内联显示 |

---

## 开发者快速上手

### 运行 & 测试

```bash
# 构建
dotnet build ClawSharp.slnx

# 运行所有测试
dotnet test ClawSharp.slnx

# 仅运行 ThreadSpace 相关测试
dotnet test ClawSharp.slnx --filter "FullyQualifiedName~ThreadSpace"

# 本地运行 CLI
dotnet run --project ClawSharp.CLI
```

### 数据库 Migration

本次修改需要新 EF Core migration（`20260330010000_AddGlobalThreadSpace`）。**不需要手动执行 `dotnet ef`**，`SqliteDatabaseInitializer` 在应用启动时自动应用 pending migrations。

---

## 关键代码变更路径

### 1. Lib 数据模型变更

```
ClawSharp.Lib/Runtime/ThreadSpaceContracts.cs
  - BoundFolderPath: string → string?
  - IsInit → IsGlobal

ClawSharp.Lib/Runtime/Persistence/Entities/ThreadSpaceEntity.cs
  - BoundFolderPath: required string → string?
  - IsInit → IsGlobal

ClawSharp.Lib/Runtime/Persistence/Configurations/ThreadSpaceEntityConfiguration.cs
  - is_init → is_global
  - BoundFolderPath nullable
  - unique index 加 WHERE IS NOT NULL

ClawSharp.Lib/Runtime/Persistence/Mapping/RuntimeEntityMapper.cs
  - entity.IsInit → entity.IsGlobal
```

### 2. 新 EF Core Migration

```
ClawSharp.Lib/Runtime/Persistence/Migrations/20260330010000_AddGlobalThreadSpace.cs
ClawSharp.Lib/Runtime/Persistence/Migrations/ClawDbContextModelSnapshot.cs (更新)
```

### 3. ThreadSpaceManager 变更

```
ClawSharp.Lib/Runtime/SqliteStores.cs (ThreadSpaceManager)
  - EnsureDefaultAsync: BoundFolderPath = null, IsGlobal = true
  - GetInitAsync: 按 IsGlobal=true 查找
  - CreateAsync: BoundFolderPath 可选
```

### 4. CLI 变更

```
ClawSharp.CLI/Program.cs
  - rootCommand.SetHandler(ChatCommand.RunAsync, ...)

ClawSharp.CLI/Commands/ChatCommand.cs
  - 新增欢迎头部
  - 新增 /help, /new, /resume, /cd, /home 命令
  - 改进提示符（显示 ThreadSpace 名称）
  - 未知命令友好提示
```

---

## 测试场景

### 场景 1: 零配置首次启动

```bash
# 在全新目录运行（无数据库）
CLAWSHARP_BASE=/tmp/test-claw dotnet run --project ClawSharp.CLI
# 期望：显示欢迎头部，提示符 [global] >
```

### 场景 2: 切换目录上下文

```
[global] > /cd /Users/me/myproject
# 期望：提示符变为 [myproject] >
[myproject] > /home
# 期望：提示符变回 [global] >
```

### 场景 3: 恢复会话

```
[global] > Hello
# ... agent 回复 ...
[global] > /quit

# 再次启动
claw
[global] > /resume
# 期望：加载上次 session 历史摘要，继续对话
```

### 场景 4: 帮助与未知命令

```
[global] > /help
# 期望：显示命令表格，不退出
[global] > /foo
# 期望：显示 "Unknown command: /foo. Type /help..."
```
