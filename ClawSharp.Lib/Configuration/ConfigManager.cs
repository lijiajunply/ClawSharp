using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace ClawSharp.Lib.Configuration;

/// <summary>
/// <see cref="IConfigManager"/> 的默认实现。
/// </summary>
public sealed class ConfigManager : IConfigManager
{
    private readonly IConfiguration _configuration;
    private readonly ClawOptions _options;
    private readonly string _localJsonPath;

    /// <summary>
    /// 初始化 <see cref="ConfigManager"/> 类的新实例。
    /// </summary>
    /// <param name="configuration">当前配置根。</param>
    /// <param name="options">强类型配置对象。</param>
    /// <param name="localJsonPath">本地 JSON 文件路径（可选，默认为 appsettings.Local.json）。</param>
    public ConfigManager(IConfiguration configuration, ClawOptions options, string? localJsonPath = null)
    {
        _configuration = configuration;
        _options = options;
        // 约定使用 appsettings.Local.json 作为持久化目标
        _localJsonPath = localJsonPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.Local.json");
    }

    /// <inheritdoc/>
    public Task<IReadOnlyDictionary<string, string?>> GetAllAsync(bool reload = false)
    {
        if (reload && _configuration is IConfigurationRoot root)
        {
            root.Reload();
        }

        var result = new Dictionary<string, string?>();
        foreach (var entry in _configuration.AsEnumerable())
        {
            result[entry.Key] = entry.Value;
        }
        return Task.FromResult<IReadOnlyDictionary<string, string?>>(result);
    }

    /// <inheritdoc/>
    public string? Get(string key) => _configuration[key];

    /// <inheritdoc/>
    public Task<IEnumerable<string>> GetSupportedKeysAsync()
    {
        var keys = new List<string>();
        DiscoverKeys(typeof(ClawOptions), "", keys);
        return Task.FromResult<IEnumerable<string>>(keys);
    }

    private void DiscoverKeys(Type type, string prefix, List<string> keys)
    {
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var key = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}:{prop.Name}";
            
            // 如果是基本类型或字符串，则视为配置键
            if (prop.PropertyType.IsPrimitive || prop.PropertyType == typeof(string) || prop.PropertyType == typeof(decimal))
            {
                keys.Add(key);
            }
            // 如果是类且不是集合，则递归
            else if (prop.PropertyType.IsClass && !typeof(System.Collections.IEnumerable).IsAssignableFrom(prop.PropertyType))
            {
                DiscoverKeys(prop.PropertyType, key, keys);
            }
        }
    }

    /// <inheritdoc/>
    public async Task SetAsync(string key, string? value)
    {
        // 1. 验证键是否存在
        var supportedKeys = await GetSupportedKeysAsync();
        if (!supportedKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
        {
            if (!IsKeyPotentiallyValid(key, supportedKeys))
            {
                throw new ArgumentException($"Unsupported configuration key: {key}");
            }
        }

        // 2. 更新物理存储
        await SaveToLocalJsonAsync(key, value);
    }

    private bool IsKeyPotentiallyValid(string key, IEnumerable<string> supportedKeys)
    {
        if (supportedKeys.Contains(key, StringComparer.OrdinalIgnoreCase)) return true;
        
        // 简单启发式：处理带索引的键，如 Providers:Models:0:ApiKey
        var parts = key.Split(':');
        if (parts.Length > 1)
        {
             return true; 
        }

        return false;
    }

    private async Task SaveToLocalJsonAsync(string key, string? value)
    {
        Dictionary<string, object> jsonData;
        if (File.Exists(_localJsonPath))
        {
            var content = await File.ReadAllTextAsync(_localJsonPath);
            jsonData = JsonSerializer.Deserialize<Dictionary<string, object>>(content) ?? new();
        }
        else
        {
            jsonData = new();
        }

        UpdateJsonData(jsonData, key.Split(':'), value);

        var options = new JsonSerializerOptions { WriteIndented = true };
        await File.WriteAllTextAsync(_localJsonPath, JsonSerializer.Serialize(jsonData, options));
    }

    private void UpdateJsonData(Dictionary<string, object> data, string[] path, string? value)
    {
        if (path.Length == 1)
        {
            if (value == null) data.Remove(path[0]);
            else data[path[0]] = value;
            return;
        }

        if (!data.TryGetValue(path[0], out var nestedObj) || nestedObj is not JsonElement nestedElement || nestedElement.ValueKind != JsonValueKind.Object)
        {
            var newNested = new Dictionary<string, object>();
            data[path[0]] = newNested;
            UpdateJsonData(newNested, path.Skip(1).ToArray(), value);
        }
        else
        {
            var newNested = JsonSerializer.Deserialize<Dictionary<string, object>>(nestedElement.GetRawText()) ?? new();
            data[path[0]] = newNested;
            UpdateJsonData(newNested, path.Skip(1).ToArray(), value);
        }
    }

    /// <inheritdoc/>
    public Task ResetAsync(bool all = false, string? key = null, bool force = false)
    {
        if (all)
        {
            if (File.Exists(_localJsonPath)) File.Delete(_localJsonPath);
        }
        else if (!string.IsNullOrEmpty(key))
        {
             return SaveToLocalJsonAsync(key, null);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public bool IsSecret(string key)
    {
        var secretPatterns = new[] { "ApiKey", "Token", "Secret", "Key" };
        var parts = key.Split(':');
        var lastPart = parts.Last();
        
        return secretPatterns.Any(p => lastPart.EndsWith(p, StringComparison.OrdinalIgnoreCase));
    }
}
