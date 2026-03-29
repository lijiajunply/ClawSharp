using ClawSharp.Lib.Agents;
using ClawSharp.Lib.Core;
using ClawSharp.Lib.Skills;

namespace ClawSharp.Lib.Tests;

public sealed class RegistryTests
{
    [Fact]
    public async Task AgentRegistry_DuplicateIds_Throws()
    {
        var store = new FakeAgentStore(
        [
            new AgentDefinition("dup", "One", "Desc", "gpt", "prompt", [], [], "workspace", [], Tools.ToolPermissionSet.Empty, "v1", ""),
            new AgentDefinition("dup", "Two", "Desc", "gpt", "prompt", [], [], "workspace", [], Tools.ToolPermissionSet.Empty, "v1", "")
        ]);

        var registry = new AgentRegistry(store);
        await Assert.ThrowsAsync<ValidationException>(() => registry.ReloadAsync());
    }

    [Fact]
    public async Task SkillRegistry_LoadsDefinitions()
    {
        var store = new FakeSkillStore(
        [
            new SkillDefinition("skill.one", "One", "Desc", [], [], [], [], [], "scripts/run.sh", "v1", "")
        ]);

        var registry = new SkillRegistry(store);
        await registry.ReloadAsync();

        Assert.Single(registry.GetAll());
        Assert.Equal("skill.one", registry.Get("skill.one").Id);
    }

    private sealed class FakeAgentStore(IReadOnlyList<AgentDefinition> agents) : IAgentDefinitionStore
    {
        public Task<IReadOnlyList<AgentDefinition>> LoadAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(agents);
    }

    private sealed class FakeSkillStore(IReadOnlyList<SkillDefinition> skills) : ISkillDefinitionStore
    {
        public Task<IReadOnlyList<SkillDefinition>> LoadAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(skills);
    }
}
