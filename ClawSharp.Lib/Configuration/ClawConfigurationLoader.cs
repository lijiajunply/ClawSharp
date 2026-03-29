using Microsoft.Extensions.Configuration;

namespace ClawSharp.Lib.Configuration;

/// <summary>
/// 负责按约定顺序构建 ClawSharp 配置树。
/// </summary>
public static class ClawConfigurationLoader
{
    /// <summary>
    /// 使用 JSON、<c>.env</c>、环境变量和运行时覆盖项构建配置根。
    /// </summary>
    /// <param name="basePath">配置文件解析时使用的基准目录。</param>
    /// <param name="overrides">最终追加的内存覆盖项，优先级最高。</param>
    /// <param name="primaryJson">主配置文件名，默认是 <c>appsettings.json</c>。</param>
    /// <param name="localJson">本地覆盖配置文件名，默认是 <c>appsettings.Local.json</c>。</param>
    /// <param name="dotEnv">dotenv 文件名，默认是 <c>.env</c>。</param>
    /// <returns>按既定顺序合成后的 <see cref="IConfigurationRoot"/>。</returns>
    public static IConfigurationRoot Build(
        string basePath,
        IDictionary<string, string?>? overrides = null,
        string primaryJson = "appsettings.json",
        string localJson = "appsettings.Local.json",
        string dotEnv = ".env")
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile(primaryJson, optional: true, reloadOnChange: false)
            .AddJsonFile(localJson, optional: true, reloadOnChange: false)
            .AddDotEnvFile(dotEnv, optional: true)
            .AddEnvironmentVariables();

        if (overrides is not null)
        {
            builder.AddInMemoryCollection(overrides);
        }

        return builder.Build();
    }
}
