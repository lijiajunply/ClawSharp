# CLI 命令规范：/speckit

## 1. 基础命令
- `/speckit`：显示当前功能的 SpecKit 进度和可用操作。

## 2. 功能生命周期
- `/speckit init <name>`：
  - 触发 `create-new-feature.sh` 逻辑。
  - 创建规范目录并生成 `spec.md`。

- `/speckit plan`：
  - 对当前功能的 `spec.md` 进行规划。
  - 触发 `speckit.plan` agent。

- `/speckit scaffold`：
  - 手动触发脚手架生成。
  - 解析 `plan.md`，展示建议动作，并等待确认。

## 3. 自举交互 (Hook)
- **当 `plan.md` 保存时**：
  - ClawSharp CLI 检测到变更。
  - 内部调用 `IScaffoldAnalyzer`。
  - 在控制台输出：
    > 🎨 检测到 [002-speckit-integration] 的技术计划已更新。
    > 建议：创建分支 [luckyfish/002-speckit-integration] 并生成 5 个占位文件。
    > 是否执行脚手架生成？[y/N]
