# 快速入门：SpecKit 驱动开发

## 1. 初始化项目
在 ClawSharp CLI 中运行：
```bash
/init-proj --name "MyNewAgent" --type paper
```
生成的项目将自动包含 `.specify` 目录及其核心治理模板。

## 2. 规范阶段 (Spec Phase)
1. 在项目根目录运行 `/speckit init "My Feature"`。
2. 编辑生成的 `specs/XXX/spec.md` 描述功能需求。

## 3. 规划阶段 (Plan Phase)
运行 `/speckit plan`。AI 将生成 `plan.md`，包含技术选型和架构设计。

## 4. 自举脚手架 (Self-Scaffolding)
1. 确认 `plan.md` 中的 `### Source Code` 部分包含你要创建的文件。
2. 运行 `speckit scaffold path/to/plan.md` 预览建议变更，或运行 `speckit watch path/to/plan.md` 进入监听模式。
3. 当 CLI 提示“是否执行脚手架生成？”时，输入 `y`；若在自动化场景中可改用 `speckit scaffold --yes path/to/plan.md`。
4. **结果**：系统自动创建 Git 分支、对应文件夹、占位代码文件，并在 `tasks.md` 中同步 Planner 生成的任务区块。

## 5. 实施阶段 (Implementation Phase)
按照 `tasks.md` 中的任务进行编码，并在完成后把相应项目标记为已完成。
