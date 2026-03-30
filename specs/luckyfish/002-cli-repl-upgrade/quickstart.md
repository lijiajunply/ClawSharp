# 快速入门：REPL 2.0 交互升级

REPL 2.0 显著增强了 CLI 的交互体验，提供了工具检查、会话管理和多行输入等功能。

## 1. 查看工具 (/tools)

在对话过程中，您可以随时查看当前 Agent 可以调用的工具及其权限。

```bash
Global > /tools
```

**示例输出**:
| 工具名 | 描述 | 权限状态 |
| :--- | :--- | :--- |
| `ls` | 列出当前目录的文件 | 始终允许 |
| `write_file` | 将内容写入文件 | 需要询问 |
| `web_search` | 执行 Google 搜索 | 始终允许 |

## 2. 管理会话 (/sessions)

您可以查看当前 ThreadSpace 下的历史会话，并快速切换。

### 列出所有会话
```bash
Global > /sessions
```

**示例输出**:
| # | 会话 ID | Agent | 开始时间 | 最后消息预览 |
| :--- | :--- | :--- | :--- | :--- |
| 1 | `a1b2c3d4` | code-agent | 2026-03-31 10:00 | "如何优化这段代码？" |
| 2 | `e5f6g7h8` | spec-agent | 2026-03-31 10:15 | "生成 REPL 2.0 规范" |

### 切换到特定会话
```bash
Global > /sessions 1
```

## 3. 多行输入支持

当您需要发送长提示词或代码片段时，可以使用以下方式：

### 终端内粘贴 (/paste)
```bash
Global > /paste
Paste > 这里是
Paste > 多行
Paste > 内容
Paste > . (输入 . 并按回车提交)
```

### 外部编辑器 (/edit)
```bash
Global > /edit
# 系统将打开默认编辑器 (如 vim 或 notepad)
# 在编辑器中编写内容，保存并退出后内容将自动发送。
```

## 4. 语法高亮渲染

Agent 返回的所有 Markdown 代码块都将自动进行语法高亮处理。

```bash
Agent > 以下是 C# 代码：
```csharp
public class HelloWorld {
    public static void Main() {
        System.Console.WriteLine("Hello!");
    }
}
``` (终端中将以彩色渲染)
```
