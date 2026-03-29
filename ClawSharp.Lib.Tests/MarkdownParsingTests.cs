using ClawSharp.Lib.Agents;
using ClawSharp.Lib.Core;
using ClawSharp.Lib.Skills;

namespace ClawSharp.Lib.Tests;

public sealed class MarkdownParsingTests
{
    [Fact]
    public void AgentMarkdown_ParsesSuccessfully()
    {
        const string markdown = """
---
id: planner
name: Planner
description: Plans work
model: gpt-5
system_prompt: You are a planner
tools:
  - file.read
skills:
  - summarize
memory_scope: workspace
mcp_servers:
  - filesystem
permissions:
  capabilities:
    - file.read
version: v1
---
Agent body.
""";

        var parser = new MarkdownAgentParser();
        var result = parser.Parse(markdown);

        Assert.Equal("planner", result.Id);
        Assert.Equal("Agent body.", result.Body);
        Assert.Contains("file.read", result.Tools);
    }

    [Fact]
    public void SkillMarkdown_ParsesSuccessfully()
    {
        const string markdown = """
---
id: summarize
name: Summarize
description: Summaries
inputs:
  - markdown
outputs:
  - summary
dependencies:
  - fetch
required_tools:
  - file.read
required_mcp_servers:
  - filesystem
entry: scripts/run.sh
version: v1
---
Skill body.
""";

        var parser = new MarkdownSkillParser();
        var result = parser.Parse(markdown);

        Assert.Equal("summarize", result.Id);
        Assert.Equal("scripts/run.sh", result.Entry);
        Assert.Contains("file.read", result.RequiredTools);
    }

    [Fact]
    public void AgentMarkdown_MissingFields_Throws()
    {
        const string markdown = """
---
id: bad
name: Missing
---
body
""";

        var parser = new MarkdownAgentParser();
        Assert.Throws<ValidationException>(() => parser.Parse(markdown));
    }
}
