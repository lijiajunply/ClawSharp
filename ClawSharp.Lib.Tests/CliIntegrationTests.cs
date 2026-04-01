using ClawSharp.Lib.Agents;
using ClawSharp.CLI.Commands;
using ClawSharp.CLI.Infrastructure;
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ClawSharp.Lib.Tests;

public sealed class CliIntegrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "claw-cli-tests", Guid.NewGuid().ToString("N"));

    public CliIntegrationTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private IHost CreateHost()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.AddClawSharp(builder =>
                {
                    builder.BasePath = _root;
                    builder.Override("Runtime:WorkspaceRoot", _root);
                    builder.Override("Sessions:DatabasePath", Path.Combine(_root, "cli.db"));
                    builder.Override("Providers:DefaultProvider", "stub");
                });
                
                // Ensure a stub agent exists for testing
                services.AddSingleton<IAgentRegistry>(sp => {
                    var agent = new AgentDefinition(
                        "planner", "Planner", "desc", "", "stub-model", "sys", [], [], "workspace", [], Tools.ToolPermissionSet.Empty, "v1", "");
                    return new FakeAgentRegistry(agent);
                });
            })
            .Build();
    }

    [Fact]
    public async Task Cli_Init_CreatesDirectoryAndDatabase()
    {
        using var host = CreateHost();
        var runtime = host.Services.GetRequiredService<IClawRuntime>();

        await runtime.InitializeAsync();

        Assert.True(Directory.Exists(_root));
    }

    [Fact]
    public async Task Cli_Chat_CanStartSession()
    {
        using var host = CreateHost();
        var runtime = host.Services.GetRequiredService<IClawRuntime>();
        await runtime.InitializeAsync();

        var session = await runtime.StartSessionAsync("planner");
        Assert.NotNull(session);
        Assert.Equal("planner", session.Record.AgentId);
    }

    [Fact]
    public async Task MultilineInputCollector_CapturePasteAsync_JoinsLinesUntilSentinel()
    {
        var inputs = new Queue<string?>(["first line", "second line", ".", "ignored"]);

        var result = await MultilineInputCollector.CapturePasteAsync(_ => Task.FromResult(inputs.Dequeue()), "Paste > ");

        Assert.Equal($"first line{Environment.NewLine}second line", result);
    }

    [Fact]
    public void ChatCommand_CreateEditorStartInfo_UsesEditorCommandAndFilePath()
    {
        const string editor = "code --wait";
        const string filePath = "/tmp/test-file.md";

        var startInfo = ChatCommand.CreateEditorStartInfo(editor, filePath);

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal("cmd.exe", startInfo.FileName);
            Assert.Contains(editor, startInfo.Arguments);
            Assert.Contains(filePath, startInfo.Arguments);
            return;
        }

        Assert.Equal("/bin/sh", startInfo.FileName);
        Assert.Equal(2, startInfo.ArgumentList.Count);
        Assert.Equal("-lc", startInfo.ArgumentList[0]);
        Assert.Contains(editor, startInfo.ArgumentList[1]);
        Assert.Contains(filePath, startInfo.ArgumentList[1]);
    }

    [Fact]
    public void MarkdownRenderer_DetectsRichMarkdownStructures()
    {
        var markdown = new ClawSharp.CLI.Infrastructure.Markdown("## Heading");

        Assert.True(markdown.HasRichContent);

        markdown.Update("Plain text only");

        Assert.False(markdown.HasRichContent);
    }

    [Fact]
    public void ChatCommand_SupportsAgentsSlashCommand()
    {
        Assert.True(ChatCommand.SupportsSlashCommand("/agents"));
        Assert.False(ChatCommand.SupportsSlashCommand("/definitely-not-a-command"));
    }

}

internal class FakeAgentRegistry : IAgentRegistry
{
    private readonly AgentDefinition _agent;
    public FakeAgentRegistry(AgentDefinition agent) => _agent = agent;

    public IAgentDefinitionStore Store => new FakeStore(_agent);

    public Task ReloadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public IReadOnlyCollection<AgentDefinition> GetAll() => [_agent];

    public AgentDefinition Get(string id) => id == _agent.Id ? _agent : throw new KeyNotFoundException();

    private class FakeStore : IAgentDefinitionStore
    {
        private readonly AgentDefinition _agent;
        public FakeStore(AgentDefinition agent) => _agent = agent;
        public Task<AgentDefinition?> GetAsync(string id) => Task.FromResult<AgentDefinition?>(id == _agent.Id ? _agent : null);
        public Task<IReadOnlyList<AgentDefinition>> ListAsync() => Task.FromResult<IReadOnlyList<AgentDefinition>>([_agent]);
        public Task<IReadOnlyList<AgentDefinition>> LoadAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AgentDefinition>>([_agent]);
    }
}
