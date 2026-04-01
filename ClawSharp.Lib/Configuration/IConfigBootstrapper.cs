namespace ClawSharp.Lib.Configuration;

using ClawSharp.Lib.Runtime;

/// <summary>
/// 提供初始化配置文件的模板和持久化逻辑。
/// </summary>
public interface IConfigBootstrapper
{
    /// <summary>
    /// 获取当前支持的所有模型提供商模板。
    /// </summary>
    IEnumerable<ProviderTemplate> GetProviderTemplates(EnvironmentDiscoveryResult? discovery = null);

    /// <summary>
    /// 根据用户输入生成完整的 appsettings.json 字符串。
    /// </summary>
    string GenerateConfigJson(BootstrapConfig input);

    /// <summary>
    /// 将配置保存到指定路径。
    /// </summary>
    Task SaveConfigAsync(string path, string json);
}
