# 数据模型：REPL 2.0 升级

本功能不引入新的持久化实体，但依赖于 `ClawSharp.Lib` 中现有的核心实体。以下是本功能涉及的关键数据模型及其在 REPL 中的用途。

## 1. 会话记录 (SessionRecord)

存储在 SQLite 数据库中，用于会话管理。

| 字段 | 类型 | 说明 | 用途 |
| :--- | :--- | :--- | :--- |
| `SessionId` | `string` | 会话唯一标识 | 用于切换会话 |
| `ThreadSpaceId` | `string` | 所属空间标识 | 用于过滤当前空间的会话 |
| `AgentId` | `string` | 关联 Agent 标识 | 显示会话关联的 Agent |
| `StartedAt` | `DateTimeOffset` | 启动时间 | 在 `/sessions` 列表中显示 |
| `Status` | `enum` | 会话状态 | 显示会话是否已完成或正在运行 |

## 2. 授权工具 (Authorized Tools)

在启动 Agent 进程前解析出的工具集合。

| 字段 | 说明 | 用途 |
| :--- | :--- | :--- |
| `Name` | 工具名称 | 在 `/tools` 列表中显示 |
| `Description` | 工具描述 | 让用户了解工具功能 |
| `Capabilities` | 工具所需能力 | 显示工具的权限要求 |

## 3. 历史消息 (PromptMessage)

用于在切换会话后回放历史。

| 字段 | 类型 | 说明 | 用途 |
| :--- | :--- | :--- | :--- |
| `Role` | `enum` | 角色 (User/Assistant/Tool) | 区分历史消息来源 |
| `Content` | `string` | 消息文本内容 | 回放时的主要显示内容 |
| `CreatedAt` | `DateTimeOffset` | 创建时间 | 排序依据 |

## 4. 运行时配置 (ClawOptions)

| 字段 | 说明 | 用途 |
| :--- | :--- | :--- |
| `WorkspacePolicy` | 定义强制工具和全局权限策略 | 决定 `/tools` 的显示结果 |
