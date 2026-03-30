using ClawSharp.Lib.Agents;
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Skills;

namespace ClawSharp.Lib.Tests;

public class RegistryTests : IDisposable
{
    private readonly string _userAgentPath;
    private readonly string _userSkillPath;
    private readonly ClawOptions _options;

    public RegistryTests()
    {
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _userAgentPath = Path.Combine(userHome, ".agent");
        _userSkillPath = Path.Combine(userHome, ".skills");

        Directory.CreateDirectory(_userAgentPath);
        Directory.CreateDirectory(_userSkillPath);

        _options = new ClawOptions();
        _options.Runtime.WorkspaceRoot = Directory.GetCurrentDirectory();
    }

    [Fact]
    public async Task AgentRegistry_ShouldLoadFromUserHome()
    {
        var testFile = Path.Combine(_userAgentPath, "test-agent.md");
        var content = """
            ---
            id: user-agent-1
            name: User Agent
            description: Test
            system_prompt: Hello
            memory_scope: default
            version: 1.0
            ---
            Body
            """;
        await File.WriteAllTextAsync(testFile, content);

        try
        {
            var store = new FileSystemAgentDefinitionStore(_options);
            var registry = new AgentRegistry(store);
            await registry.ReloadAsync();

            var agents = registry.GetAll();
            Assert.Contains(agents, a => a.Id == "user.user-agent-1" && a.Source == Core.DynamicSourceType.User);
        }
        finally
        {
            if (File.Exists(testFile)) File.Delete(testFile);
        }
    }

    [Fact]
    public async Task SkillRegistry_ShouldApplyUserPrefix()
    {
        var testFile = Path.Combine(_userSkillPath, "test-skill.md");
        var content = """
            ---
            id: my-skill
            name: User Skill
            description: Test
            entry: main.js
            version: 1.0
            ---
            Body
            """;
        await File.WriteAllTextAsync(testFile, content);

        try
        {
            var store = new FileSystemSkillDefinitionStore(_options);
            var registry = new SkillRegistry(store);
            await registry.ReloadAsync();

            var skills = registry.GetAll();
            Assert.Contains(skills, s => s.Id == "user.my-skill" && s.Source == Core.DynamicSourceType.User);
        }
        finally
        {
            if (File.Exists(testFile)) File.Delete(testFile);
        }
    }

    public void Dispose()
    {
        // We don't delete the directories themselves as they might be used by the user,
        // but for testing in CI or a clean environment, it might be safer.
        // For this local environment, we only cleanup files we created.
    }
}
