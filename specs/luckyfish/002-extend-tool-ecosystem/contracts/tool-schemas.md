# 工具契约：web_browser

## 输入 Schema
```json
{
  "type": "object",
  "properties": {
    "url": { "type": "string", "description": "目标 URL" },
    "wait_selector": { "type": "string", "description": "可选：等待元素出现" },
    "wait_time": { "type": "integer", "description": "可选：硬性等待时间（毫秒）" }
  },
  "required": ["url"]
}
```

# 工具契约：csv_read

## 输入 Schema
```json
{
  "type": "object",
  "properties": {
    "path": { "type": "string", "description": "CSV 路径" },
    "limit": { "type": "integer", "default": 50 },
    "offset": { "type": "integer", "default": 0 },
    "has_header": { "type": "boolean", "default": true }
  },
  "required": ["path"]
}
```
