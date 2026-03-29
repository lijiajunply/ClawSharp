using Microsoft.Extensions.Configuration;

namespace ClawSharp.Lib.Configuration;

internal sealed class DotEnvConfigurationSource : IConfigurationSource
{
    public string Path { get; init; } = ".env";

    public bool Optional { get; init; } = true;

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        var fileProvider = builder.GetFileProvider();
        var fileInfo = fileProvider.GetFileInfo(Path);
        return new DotEnvConfigurationProvider(fileInfo.PhysicalPath, Optional);
    }
}

internal sealed class DotEnvConfigurationProvider(string? path, bool optional) : ConfigurationProvider
{
    public override void Load()
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            if (optional)
            {
                return;
            }

            throw new FileNotFoundException("DotEnv file was not found.", path);
        }

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim().Trim('"');
            Data[key.Replace("__", ":", StringComparison.Ordinal)] = value;
        }
    }
}

public static class DotEnvConfigurationExtensions
{
    public static IConfigurationBuilder AddDotEnvFile(
        this IConfigurationBuilder builder,
        string path = ".env",
        bool optional = true)
    {
        return builder.Add(new DotEnvConfigurationSource
        {
            Path = path,
            Optional = optional
        });
    }
}
