using System.Collections.Concurrent;
using ClawSharp.Lib.Agents;
using ClawSharp.Lib.Skills;

namespace ClawSharp.Lib.Runtime;

/// <summary>
/// Watches for changes in agent and skill directories and triggers reloads.
/// </summary>
public sealed class DefinitionWatcher : IDisposable
{
    private readonly IAgentRegistry _agents;
    private readonly ISkillRegistry _skills;
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastReload = new();
    private static readonly TimeSpan DebounceTime = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// 当定义文件变更并完成重新加载后触发。
    /// </summary>
    public event Action<DefinitionChangedEvent>? DefinitionChanged;

    /// <summary>
    /// 初始化 <see cref="DefinitionWatcher"/> 类的新实例。
    /// </summary>
    /// <param name="agents">Agent 注册表。</param>
    /// <param name="skills">Skill 注册表。</param>
    public DefinitionWatcher(IAgentRegistry agents, ISkillRegistry skills)
    {
        _agents = agents;
        _skills = skills;
    }

    /// <summary>
    /// 开始监视指定路径下的定义文件。
    /// </summary>
    /// <param name="path">要监视的目录路径。</param>
    /// <param name="isAgent">指定该目录是否包含 agent 定义。</param>
    public void Watch(string path, bool isAgent)
    {
        if (!Directory.Exists(path)) return;

        var watcher = new FileSystemWatcher(path, "*.md")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        watcher.Changed += (_, e) => OnChanged(isAgent, e.FullPath);
        watcher.Created += (_, e) => OnChanged(isAgent, e.FullPath);
        watcher.Deleted += (_, e) => OnChanged(isAgent, e.FullPath);
        watcher.Renamed += (_, e) => OnChanged(isAgent, e.FullPath);

        _watchers.Add(watcher);
    }

    private void OnChanged(bool isAgent, string fullPath)
    {
        var key = isAgent ? "agent" : "skill";
        var now = DateTime.UtcNow;
        
        if (_lastReload.TryGetValue(key, out var last) && now - last < DebounceTime)
        {
            return;
        }

        _lastReload[key] = now;

        _ = Task.Run(async () =>
        {
            await Task.Delay(DebounceTime); // Extra safety delay
            try
            {
                if (isAgent) await _agents.ReloadAsync();
                else await _skills.ReloadAsync();

                DefinitionChanged?.Invoke(new DefinitionChangedEvent(isAgent, fullPath));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Watcher] Failed to reload {(isAgent ? "agents" : "skills")}: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// 释放文件系统监视器资源。
    /// </summary>
    public void Dispose()
    {
        foreach (var watcher in _watchers)
        {
            watcher.Dispose();
        }
        _watchers.Clear();
    }
}

/// <summary>
/// 定义文件变更事件。
/// </summary>
/// <param name="IsAgent">是否为 Agent 定义。</param>
/// <param name="FullPath">发生变更的文件路径。</param>
public sealed record DefinitionChangedEvent(bool IsAgent, string FullPath);
