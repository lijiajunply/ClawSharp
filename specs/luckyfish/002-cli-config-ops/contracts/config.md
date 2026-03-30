# Contract: IConfigManager

## Namespace: `ClawSharp.Lib.Configuration`

```csharp
namespace ClawSharp.Lib.Configuration;

/// <summary>
/// 管理 ClawSharp 配置的读取、修改与持久化。
/// </summary>
public interface IConfigManager
{
    /// <summary>
    /// 获取当前加载的所有配置项（平铺键值对）。
    /// </summary>
    /// <param name="reload">是否重新从物理存储加载。</param>
    Task<IReadOnlyDictionary<string, string?>> GetAllAsync(bool reload = false);

    /// <summary>
    /// 获取特定配置项的值。
    /// </summary>
    string? Get(string key);

    /// <summary>
    /// 获取所有支持的配置键列表（通过反射 ClawOptions 自动推导）。
    /// </summary>
    Task<IEnumerable<string>> GetSupportedKeysAsync();

    /// <summary>
    /// 设置配置项并持久化。
    /// </summary>
    /// <param name="key">配置键，例如 "Providers:DefaultProvider"。</param>
    /// <param name="value">配置值。</param>
    /// <returns>操作结果。</returns>
    Task SetAsync(string key, string? value);

    /// <summary>
    /// 重置配置到默认值。
    /// </summary>
    /// <param name="all">是否重置所有配置。</param>
    /// <param name="key">指定重置的键，若 all 为 true 则忽略。</param>
    /// <param name="force">为 true 时跳过交互式确认（CLI 适用）。</param>
    Task ResetAsync(bool all = false, string? key = null, bool force = false);

    /// <summary>
    /// 检查一个键是否被视为敏感信息。
    /// </summary>
    bool IsSecret(string key);
}
```

## CLI Interface (Contract)

### Command: `config`
Parent command for configuration operations.

#### Subcommand: `list`
- Description: "Show all configuration settings."
- Options:
  - `--all`: Show all keys (including those not set).
- Output: Table with Key, Value (masked), and Source.

#### Subcommand: `get <key>`
- Description: "Retrieve the value of a specific setting."
- Arguments: `key` (Required).

#### Subcommand: `set <key> [value]`
- Description: "Set a configuration value."
- Arguments: 
  - `key` (Required). MUST be validated against supported keys.
  - `value` (Optional - triggers interactive prompt if missing and secret).

#### Subcommand: `reset [--all] [--key <key>] [--force]`
- Description: "Reset configuration to defaults."
- Options:
  - `--all`: Reset everything.
  - `--key`: Reset a specific key.
  - `--force`: Skip confirmation prompt.
- Interactive Behavior: If `--force` is absent, MUST use `AnsiConsole.Confirm` before proceeding.
