# 快速入门：扩展工具集

## 如何使用新工具

AI 可以通过以下工具执行更复杂的任务：

### 1. 网页浏览 (`web_browser`)
```json
{
  "url": "https://news.ycombinator.com",
  "wait_selector": ".itemlist"
}
```

### 2. 读取表格 (`csv_read`)
```json
{
  "path": "data/sales_2024.csv",
  "limit": 100,
  "offset": 0
}
```

### 3. Git 操作 (`git_ops`)
```json
{
  "operation": "status"
}
```

### 4. 阅读文档 (`pdf_read`)
```json
{
  "path": "docs/specification.pdf",
  "pages": [1, 2, 3]
}
```

## 权限配置 (appsettings.json)
确保在权限集中启用了相应能力：
- `web_browser` 需要 `NetworkAccess`。
- `csv_read`/`pdf_read` 需要 `FileRead`。
- `git_ops` 建议新增 `VersionControl` 能力。
