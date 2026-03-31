# 快速开始：使用 `claw stats` 命令

本指南展示了如何使用新增加的 `claw stats` 命令来监控和分析 ClawSharp 会话。

## 1. 安装与设置
确保您的项目已启用 DuckDB 分析（通常在 `appsettings.json` 中配置）：
```json
"Databases": {
  "DuckDb": {
    "Enabled": true,
    "Path": "claw_analytics.db"
  }
}
```

## 2. 常用命令

### 基础统计摘要
显示过去 24 小时的 Token 消耗、活跃会话和总消息数。
```bash
claw stats
```

### 指定时间段
查看过去 7 天或 30 天的数据。
```bash
claw stats --period 7d
claw stats --period 30d
```

### 工具使用排行
查看哪些工具被调用最频繁，以及它们的成功率。
```bash
claw stats --tools
```

### Agent 性能报告
查看每个 Agent 的平均响应时长和请求数，帮助识别性能瓶颈。
```bash
claw stats --agents
```

## 3. 解读数据

### Token 消耗
- **Prompt Tokens**: 输入给模型的 Token 数。
- **Completion Tokens**: 模型生成的 Token 数。

### 性能指标
- **Latency (Avg)**: 模型的平均往返时间（RTT），包括网络延迟和推理时间。
- **Tool Success Rate**: 工具执行成功的比例。如果某个工具失败率高，请检查其定义。
