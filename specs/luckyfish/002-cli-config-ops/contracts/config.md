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
    IReadOnlyDictionary<string, string?> GetAll();

    /// <summary>
    /// 获取特定配置项的值。
    /// </summary>
    string? Get(string key);

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
    Task ResetAsync(bool all = false, string? key = null);

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
- Output: Table with Key, Value (masked), and Status.

#### Subcommand: `get <key>`
- Description: "Retrieve the value of a specific setting."
- Arguments: `key` (Required).

#### Subcommand: `set <key> [value]`
- Description: "Set a configuration value."
- Arguments: 
  - `key` (Required)
  - `value` (Optional - triggers interactive prompt if missing and secret)

#### Subcommand: `reset [--all] [--key <key>]`
- Description: "Reset configuration to defaults."
- Options:
  - `--all`: Reset everything.
  - `--key`: Reset a specific key.
