# Contract: Configuration Bootstrap

## Namespace: `ClawSharp.Lib.Configuration`

```csharp
namespace ClawSharp.Lib.Configuration;

/// <summary>
/// 提供初始化配置文件的模板和持久化逻辑。
/// </summary>
public interface IConfigBootstrapper
{
    /// <summary>
    /// 获取当前支持的所有模型提供商模板。
    /// </summary>
    IEnumerable<ProviderTemplate> GetProviderTemplates();

    /// <summary>
    /// 根据用户输入生成完整的 appsettings.json 字符串。
    /// </summary>
    string GenerateConfigJson(BootstrapConfig input);

    /// <summary>
    /// 将配置保存到指定路径。
    /// </summary>
    Task SaveConfigAsync(string path, string json);
}
```

## CLI Interactive Wizard (Contract)

### Interaction Flow

1. **Detection**: Check `File.Exists("appsettings.json")`.
2. **Welcome**: Display "Welcome to ClawSharp! Let's get you set up."
3. **Local Check (Optional)**: If `appsettings.Local.json` exists, show:
   - "A local configuration already exists. Do you want to use it or create a new primary config?"
4. **Workspace Root**: Prompt: "Enter workspace root path" (Default: `.`).
5. **Data Path**: Prompt: "Enter runtime data path" (Default: `.clawsharp`).
6. **Provider**: Prompt: "Select your default AI provider" (List choice).
7. **API Key**: Prompt: "Enter API Key for [Provider]" (Masked).
8. **Finalization**: "Generating appsettings.json..." -> Success message.
