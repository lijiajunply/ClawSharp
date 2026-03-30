# 数据模型：扩展工具集

## 1. WebBrowserRequest (Input)
- `url` (string): 目标网页 URL。
- `wait_selector` (string?): 渲染完成后等待的 CSS 选择器。
- `wait_time` (int?): 渲染等待时间（毫秒）。

## 2. CsvReadRequest (Input)
- `path` (string): CSV 文件相对/绝对路径。
- `limit` (int): 每页读取行数（默认 50）。
- `offset` (int): 起始行偏移量（默认 0）。
- `has_header` (bool): 文件是否包含标题行。

## 3. GitOpsRequest (Input)
- `operation` (string): `status` | `log` | `diff`。
- `target` (string?): commit ID, branch name, or file path。
- `limit` (int?): 日志条目上限。

## 4. PdfReadRequest (Input)
- `path` (string): PDF 文件路径。
- `pages` (int[]?): 要提取的页码（默认全部）。

## 5. ToolInvocationResult (Structured Payload)
- 对于 CSV：返回 `data: Array<Object>`。
- 对于 Web：返回 `content: string` (已清理的文本)。
- 对于 PDF：返回 `pages: Array<{ page: number, text: string }>`。
- 对于 Git：返回结构化的 Git 对象模型。
