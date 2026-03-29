# Data Model: ThreadSpace 重新设计

**Branch**: `luckyfish/002-redesign-threadspace-cli` | **Date**: 2026-03-30

---

## 实体变更总览

### ThreadSpaceRecord（合约层）

```csharp
// 变更前
public sealed record ThreadSpaceRecord(
    ThreadSpaceId ThreadSpaceId,
    string Name,
    string BoundFolderPath,   // required, NOT NULL
    bool IsInit,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ArchivedAt = null);

// 变更后
public sealed record ThreadSpaceRecord(
    ThreadSpaceId ThreadSpaceId,
    string Name,
    string? BoundFolderPath,  // 可选，全局 ThreadSpace 为 null
    bool IsGlobal,            // 原 IsInit，语义：是否为全局无绑定 ThreadSpace
    DateTimeOffset CreatedAt,
    DateTimeOffset? ArchivedAt = null);
```

**字段说明**:
| 字段 | 类型 | 说明 |
|------|------|------|
| `ThreadSpaceId` | `ThreadSpaceId` | 唯一标识，不可变 |
| `Name` | `string` | 人类可读名称，全局 TS 固定为 `"init"` |
| `BoundFolderPath` | `string?` | 绑定的本地目录；全局 ThreadSpace 为 `null` |
| `IsGlobal` | `bool` | `true` 表示全局无绑定 ThreadSpace；`false` 表示目录绑定 TS |
| `CreatedAt` | `DateTimeOffset` | 创建时间 UTC |
| `ArchivedAt` | `DateTimeOffset?` | 软删除时间；活跃时为 `null` |

**验证规则**:
- `IsGlobal = true` → `BoundFolderPath` 必须为 `null`
- `IsGlobal = false` → `BoundFolderPath` 必须非空
- 全局 ThreadSpace 全局只能存在一个（`IsGlobal = true`）

---

### CreateThreadSpaceRequest（合约层）

```csharp
// 变更前
public sealed record CreateThreadSpaceRequest(string Name, string BoundFolderPath)

// 变更后
public sealed record CreateThreadSpaceRequest(string Name, string? BoundFolderPath = null)
```

---

### ThreadSpaceEntity（持久化层）

```csharp
// 变更前
internal sealed class ThreadSpaceEntity
{
    public required string ThreadSpaceId { get; init; }
    public required string Name { get; set; }
    public required string BoundFolderPath { get; set; }  // NOT NULL
    public bool IsInit { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ArchivedAt { get; set; }
}

// 变更后
internal sealed class ThreadSpaceEntity
{
    public required string ThreadSpaceId { get; init; }
    public required string Name { get; set; }
    public string? BoundFolderPath { get; set; }          // NULL 允许
    public bool IsGlobal { get; set; }                    // 原 IsInit
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ArchivedAt { get; set; }
}
```

---

### ThreadSpaceEntityConfiguration 变更

```csharp
// 变更：列名 is_init → is_global，BoundFolderPath 允许 null，unique index 加 WHERE 条件
builder.Property(x => x.IsGlobal).HasColumnName("is_global");
builder.Property(x => x.BoundFolderPath).HasColumnName("bound_folder_path").HasMaxLength(1024).IsRequired(false);
builder.HasIndex(x => x.BoundFolderPath).IsUnique().HasFilter("[bound_folder_path] IS NOT NULL");
```

---

## 数据库 Migration

### `20260330010000_AddGlobalThreadSpace`

**变更内容**:
1. 将 `bound_folder_path` 列从 `NOT NULL` 改为 `NULL`
2. 将 `is_init` 列重命名为 `is_global`
3. 重建 `IX_thread_spaces_bound_folder_path` 索引，增加 `WHERE bound_folder_path IS NOT NULL` 条件

**SQLite 实现注意**: SQLite 不支持 `ALTER COLUMN`，需通过表重建实现。EF Core SQLite provider 生成标准重建步骤：
1. 建临时新表（含新 schema）
2. `INSERT INTO new SELECT ... FROM old`（`is_init` 数据迁移到 `is_global`）
3. `DROP TABLE old`
4. `RENAME new TO thread_spaces`
5. 重建索引和外键

---

## 关联变更

### `ThreadSpaceManager`（`SqliteStores.cs`）

| 方法 | 变更 |
|------|------|
| `EnsureDefaultAsync` | 创建全局 TS 时：`BoundFolderPath = null`，`IsGlobal = true`；查找改用 `IsGlobal = true` 而非 `Name = "init"` |
| `CreateAsync` | 不再强制要求 `BoundFolderPath` 非空；增加互斥校验（`IsGlobal` 存在时拒绝再创建） |
| `GetInitAsync` | 改为查询 `IsGlobal = true` 的记录 |

### `IThreadSpaceRepository`（`EfThreadSpaceRepository.cs`）

新增方法（或修改现有）：
```csharp
Task<ThreadSpaceRecord?> GetGlobalAsync(CancellationToken cancellationToken = default);
```
实现：`WHERE is_global = 1 AND archived_at IS NULL`

### `RuntimeEntityMapper`

`MapToRecord` 方法需更新：`entity.IsInit` → `entity.IsGlobal`

---

## 状态转换（全局 ThreadSpace 生命周期）

```
[首次启动]
    ↓
EnsureDefaultAsync() → 不存在 → 创建 IsGlobal=true, BoundFolderPath=null
    ↓
[使用中] ← /new、/resume、普通对话
    ↓
[/cd path] → 切换到 DirectoryThreadSpace（不影响全局 TS）
    ↓
[/home] → 切换回全局 TS
```

---

## 已有数据兼容性

- `BoundFolderPath` 由 `NOT NULL` 改为 `NULL`：**现有记录不受影响**，原来都有值，migration 后仍保持原值
- `is_init` → `is_global` 列重命名：**现有 `IsInit=true` 记录** 迁移后 `is_global = 1`，语义完全兼容
- 全局 ThreadSpace 的 `BoundFolderPath` 从原来的 `WorkspaceRoot` 变为 `null`：migration 中对 `is_init=1` 的行执行 `SET bound_folder_path = NULL`
