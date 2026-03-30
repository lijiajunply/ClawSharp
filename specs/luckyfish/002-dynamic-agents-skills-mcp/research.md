# Research Findings: Dynamic Agents, Skills, and MCP Support

## 1. 跨平台主目录解析 (Cross-platform Home Directory)

**决策**: 使用 `Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)`。
**理由**: 这是 .NET 中解析用户主目录的标准且跨平台安全的方式。在 macOS/Linux 上解析为 `/Users/name` 或 `/home/name`，在 Windows 上解析为 `C:\Users\Name`。
**替代方案**: 
- `Path.Combine(Environment.GetEnvironmentVariable("HOME"))`: 仅限 Unix。
- 使用 `~` 字符串替换: 手动替换逻辑容易出错。

## 2. 动态加载 Store 扩展 (Definition Store Extension)

**决策**: 
- 扩展 `FileSystemAgentDefinitionStore` 和 `FileSystemSkillDefinitionStore` 以支持从多个根目录扫描。
- 在用户主目录下查找 `~/.agent/*.md` 和 `~/.skills/*.md`。
**理由**: 目前的 Store 仅扫描 `WorkspaceRoot`。通过将 `~/.agent` 添加为额外的扫描源，可以实现动态发现。
**细节**: 
- 用户目录下的文件不强制要求命名为 `agent.md` 或 `SKILL.md`，任何 `.md` 文件只要包含有效的 YAML 前置配置即可被解析。

## 3. MCP 1.0 Stdio 传输实现 (MCP Stdio Transport)

**决策**: 使用 `System.Diagnostics.Process` 实现 `stdio` 传输。
**理由**: MCP 1.0 的核心是基于 JSON-RPC 的进程间通信。通过重定向 `StandardInput` 和 `StandardOutput`，可以与本地 MCP 服务器进行全双工通信。
**技术挑战**: 
- **死锁避免**: 必须异步读取输出流。
- **并发处理**: 需要一个消息队列或 `TaskCompletionSource` 映射表来匹配请求和响应。
**依赖**: `System.Text.Json` 用于协议序列化。

## 4. 技能命名空间处理 (Skill Namespacing)

**决策**: 在 `SkillRegistry` 中，如果技能定义来源于用户目录（而非内置或项目工作区），则其 `Id` 将自动添加 `user.` 前缀。
**理由**: 避免用户自定义技能与核心内置技能（如 `web-search`）发生冲突。
**逻辑**: 
- 修改 `SkillDefinition` 以包含 `Source` 属性。
- `SkillRegistry` 在加载时根据 `Source` 判断是否应用前缀。

## 5. 动态刷新机制 (Dynamic Refresh)

**决策**: 引入 `IDefinitionWatcher` 接口，封装 `FileSystemWatcher`。
**理由**: `FileSystemWatcher` 提供低延迟的文件系统变更通知。
**实现**: 
- 当 `~/.agent` 或 `~/.skills` 发生变更（创建、删除、修改）时，触发 `Registry.ReloadAsync()`。
- 为了性能，应使用防抖（Debounce）处理，避免频繁重新加载。

## 未解决问题 (Resolved in Research)

- **SSE 支持**: 确认为未来增强功能。
- **配置文件**: MCP 配置将存储在 `~/.clawsharp/mcp.json`，格式兼容 `claude_desktop_config.json` 的 `mcpServers` 部分，以便用户复用现有配置。
