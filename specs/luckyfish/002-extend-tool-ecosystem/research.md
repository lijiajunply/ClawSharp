# 研究报告：扩展工具集技术选型

## 研究任务 1：Playwright 驱动依赖最小化
**不确定项**: 是否有更轻量的 Playwright 替代方案，或如何最小化驱动依赖？

- **决策**: 采用 `Microsoft.Playwright`。
- **理由**: Playwright 是目前最稳定、功能最全的无头浏览器驱动，支持异步操作且 API 友好。
- **最小化方案**: 
    - 仅安装必要的浏览器执行文件（如 Chromium）。
    - 提供一个脚本或内置方法用于在用户机器上进行初始化安装 (`playwright install chromium`)。
- **备选方案**: 
    - `PuppeteerSharp`: 同样较重，且社区活跃度略逊于 Playwright。
    - `HtmlAgilityPack` + `HttpClient`: 无法处理 JS 渲染。

## 研究任务 2：Git 交互方案
**不确定项**: 考虑到本地优先和易用性，是使用 C# 绑定库还是直接调用系统 git 命令？

- **决策**: 优先使用内置 `ShellRunTool` 的逻辑封装，或引入 `LibGit2Sharp`。
- **理由**: 
    - `LibGit2Sharp` 不依赖外部 Git 环境，符合 "Local-First" 且自包含的原则。
    - 直接调用 `git` CLI 则依赖用户环境已安装 Git，但在处理复杂 diff 时可能更灵活。
- **最终选择**: 引入 `LibGit2Sharp` 以保证一致性和无环境依赖的体验。

## 研究任务 3：PDF 与 CSV 处理
- **CSV**: 选用 `CsvHelper`。它是 .NET 生态中事实上的标准库，支持流式读取（流式处理大文件）。
- **PDF**: 选用 `PdfPig`。完全由 C# 编写，无外部依赖，且在提取纯文本方面表现优秀。

## 研究任务 4：分页与性能
- **决策**: 为所有可能产生大数据量的工具增加 `limit` 和 `offset` 参数。
- **分页逻辑**: 
    - CSV: 按行分页。
    - PDF: 按页分页。
    - Web: 限制返回字符数（基于 `MaxOutputLength` 权限）。
