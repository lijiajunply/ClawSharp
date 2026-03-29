using Microsoft.Extensions.Configuration;

namespace ClawSharp.Lib.Configuration;

public static class ClawConfigurationLoader
{
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
