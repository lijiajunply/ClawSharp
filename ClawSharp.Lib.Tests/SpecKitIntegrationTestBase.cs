using System.Diagnostics;
using ClawSharp.Lib.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ClawSharp.Lib.Tests;

public abstract class SpecKitIntegrationTestBase : IDisposable
{
    protected SpecKitIntegrationTestBase()
    {
        Root = Path.Combine(Path.GetTempPath(), "claw-speckit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Path.Combine(Root, ".specify", "templates"));
        Directory.CreateDirectory(Path.Combine(Root, ".specify", "scripts", "bash"));
        Directory.CreateDirectory(Path.Combine(Root, ".specify", "memory"));

        File.WriteAllText(Path.Combine(Root, ".specify", "templates", "spec-template.md"), "# Spec Template\n");
        File.WriteAllText(Path.Combine(Root, ".specify", "memory", "constitution.md"), "# Constitution\n");
        File.WriteAllText(Path.Combine(Root, ".specify", "scripts", "bash", "helper.sh"), "#!/usr/bin/env bash\n");
    }

    protected string Root { get; }

    protected string FeatureRoot => Path.Combine(Root, "specs", "luckyfish", "002-demo-feature");

    protected string PlanPath => Path.Combine(FeatureRoot, "plan.md");

    protected string TasksPath => Path.Combine(FeatureRoot, "tasks.md");

    protected IHost CreateHost()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.AddClawSharp(builder =>
                {
                    builder.BasePath = Root;
                    builder.Override("Runtime:WorkspaceRoot", Root);
                    builder.Override("Sessions:DatabasePath", Path.Combine(Root, ".clawsharp", "clawsharp.db"));
                    builder.Override("Projects:SpecKitPath", Path.Combine(Root, ".specify"));
                    builder.Override("Projects:TemplatesPath", Path.Combine(Root, "workspace", "project-templates"));
                });
            })
            .Build();
    }

    protected async Task InitializeGitRepositoryAsync()
    {
        await RunGitAsync("init");
        await RunGitAsync("config user.email test@example.com");
        await RunGitAsync("config user.name Test User");
    }

    protected void WritePlan(string content)
    {
        Directory.CreateDirectory(FeatureRoot);
        File.WriteAllText(PlanPath, content);
    }

    protected static string CreateSamplePlan(string branchName = "luckyfish/002-demo-feature") =>
        $$"""
        # Implementation Plan: Demo

        **Branch**: `{{branchName}}` | **Date**: 2026-03-31

        ## Summary

        Demo plan.

        ### Source Code (repository root)

        ```text
        ClawSharp.Lib/
        └── Services/
            └── DemoService.cs
        ClawSharp.Lib.Tests/
        └── DemoServiceTests.cs
        docs/
        └── feature-overview.md
        ```

        ## Implementation Strategy

        1. Add DemoService implementation
        2. Add automated tests
        3. Document the feature
        """;

    private async Task RunGitAsync(string arguments)
    {
        var startInfo = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = Root,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start git.");
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"git {arguments} failed: {error}");
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
