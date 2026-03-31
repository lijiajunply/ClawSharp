using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Core;
using ClawSharp.Lib.Hub;
using ClawSharp.Lib.Skills;

namespace ClawSharp.Lib.Tests;

public sealed class HubTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "claw-hub-tests", Guid.NewGuid().ToString("N"));

    public HubTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task HubClient_SearchSkillsAsync_ParsesResponse()
    {
        var client = CreateClient("""
            [
              {
                "id": "pdf-toolkit",
                "name": "PDF Toolkit",
                "description": "Read and slice pdfs",
                "latestVersion": "1.2.0",
                "tags": ["pdf", "docs"],
                "downloads": 42
              }
            ]
            """);

        var results = await client.SearchSkillsAsync("pdf");

        var skill = Assert.Single(results);
        Assert.Equal("pdf-toolkit", skill.Id);
        Assert.Equal("1.2.0", skill.LatestVersion);
        Assert.Contains("pdf", skill.Tags);
    }

    [Fact]
    public async Task HubClient_GetSkillAsync_ParsesDetailResponse()
    {
        var client = CreateClient("""
            {
              "id": "pdf-toolkit",
              "name": "PDF Toolkit",
              "description": "Read and slice pdfs",
              "latestVersion": "1.2.0",
              "tags": ["pdf"],
              "downloads": 42,
              "versions": ["1.2.0", "1.1.0"],
              "readme": "# PDF Toolkit",
              "packageUrl": "https://hub.example/api/v1/skills/pdf-toolkit/versions/1.2.0/package"
            }
            """);

        var detail = await client.GetSkillAsync("pdf-toolkit");

        Assert.Equal("pdf-toolkit", detail.Id);
        Assert.Equal(2, detail.Versions.Count);
        Assert.Equal("# PDF Toolkit", detail.Readme);
    }

    [Fact]
    public async Task HubClient_DownloadSkillPackageAsync_ReadsPackageMetadata()
    {
        var packageBytes = Encoding.UTF8.GetBytes("zip-bytes");
        var client = CreateClient(packageBytes, contentType: "application/zip", fileName: "pdf-toolkit-1.2.0.zip");

        var package = await client.DownloadSkillPackageAsync("pdf-toolkit", "1.2.0");

        Assert.Equal("pdf-toolkit", package.SkillId);
        Assert.Equal("1.2.0", package.Version);
        Assert.Equal("pdf-toolkit-1.2.0.zip", package.FileName);
        Assert.Equal(packageBytes, package.Content);
    }

    [Fact]
    public async Task HubInstaller_RejectsPackageWithoutSkillFile()
    {
        var options = new ClawOptions { Hub = { InstallRoot = _root } };
        var installer = new HubInstaller(options, new HubManifestValidator(), new FakeSkillRegistry());
        var package = new HubSkillPackage("missing", "1.0.0", "missing.zip", "application/zip", CreateZipBytes(("README.md", "# nope")));

        var ex = await Assert.ThrowsAsync<ValidationException>(() => installer.InstallAsync(package, InstallTarget.UserHome));

        Assert.Contains("SKILL.md", ex.Message);
    }

    [Fact]
    public async Task HubInstaller_RejectsInvalidSkillMarkdown()
    {
        var options = new ClawOptions { Hub = { InstallRoot = _root } };
        var installer = new HubInstaller(options, new HubManifestValidator(), new FakeSkillRegistry());
        var package = new HubSkillPackage("broken", "1.0.0", "broken.zip", "application/zip", CreateZipBytes(("SKILL.md", "no frontmatter")));

        await Assert.ThrowsAsync<ValidationException>(() => installer.InstallAsync(package, InstallTarget.UserHome));
    }

    [Fact]
    public async Task HubInstaller_RejectsOverwriteWithoutForce()
    {
        var options = new ClawOptions { Hub = { InstallRoot = _root } };
        var registry = new FakeSkillRegistry();
        var installer = new HubInstaller(options, new HubManifestValidator(), registry);
        var package = CreateSkillPackage("sample-skill", "1.0.0");

        await installer.InstallAsync(package, InstallTarget.UserHome);
        var ex = await Assert.ThrowsAsync<ValidationException>(() => installer.InstallAsync(package, InstallTarget.UserHome));

        Assert.Contains("--force", ex.Message);
    }

    [Fact]
    public async Task HubInstaller_InstallsSkillDirectoryAndReloadsRegistry()
    {
        var options = new ClawOptions { Hub = { InstallRoot = _root } };
        var registry = new FakeSkillRegistry();
        var installer = new HubInstaller(options, new HubManifestValidator(), registry);
        var package = CreateSkillPackage("sample-skill", "1.0.0", ("assets/icon.txt", "asset"), ("scripts/run.sh", "echo hi"));

        var installed = await installer.InstallAsync(package, InstallTarget.UserHome);

        Assert.Equal("sample-skill", installed.SkillId);
        Assert.True(File.Exists(Path.Combine(_root, "sample-skill", "SKILL.md")));
        Assert.True(File.Exists(Path.Combine(_root, "sample-skill", "assets", "icon.txt")));
        Assert.True(File.Exists(Path.Combine(_root, "sample-skill", "scripts", "run.sh")));
        Assert.Equal(1, registry.ReloadCount);
    }

    private static HubClient CreateClient(string json)
    {
        var options = new HubOptions { BaseUrl = "https://hub.example", TimeoutSeconds = 5 };
        return new HubClient(options, () => new HttpClient(new StaticHttpMessageHandler(_ =>
            CreateResponse(HttpStatusCode.OK, Encoding.UTF8.GetBytes(json), "application/json"))));
    }

    private static HubClient CreateClient(byte[] bytes, string contentType, string fileName)
    {
        var options = new HubOptions { BaseUrl = "https://hub.example", TimeoutSeconds = 5 };
        return new HubClient(options, () => new HttpClient(new StaticHttpMessageHandler(_ =>
            CreateResponse(HttpStatusCode.OK, bytes, contentType, fileName))));
    }

    private static HttpResponseMessage CreateResponse(HttpStatusCode status, byte[] body, string contentType, string? fileName = null)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new ByteArrayContent(body)
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
            {
                FileName = fileName
            };
        }

        return response;
    }

    private static HubSkillPackage CreateSkillPackage(string skillId, string version, params (string path, string content)[] extraFiles)
    {
        var files = new List<(string path, string content)>
        {
            ("SKILL.md", $"---{Environment.NewLine}" +
                         $"id: {skillId}{Environment.NewLine}" +
                         $"name: Sample Skill{Environment.NewLine}" +
                         $"description: Sample description{Environment.NewLine}" +
                         $"entry: scripts/run.sh{Environment.NewLine}" +
                         $"version: {version}{Environment.NewLine}" +
                         $"---{Environment.NewLine}" +
                         "Body")
        };

        files.AddRange(extraFiles);
        return new HubSkillPackage(skillId, version, $"{skillId}-{version}.zip", "application/zip", CreateZipBytes(files.ToArray()));
    }

    private static byte[] CreateZipBytes(params (string path, string content)[] files)
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in files)
            {
                var entry = archive.CreateEntry(path);
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write(content);
            }
        }

        return memory.ToArray();
    }

    private sealed class StaticHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responder(request));
        }
    }

    private sealed class FakeSkillRegistry : ISkillRegistry
    {
        public int ReloadCount { get; private set; }

        public Task ReloadAsync(CancellationToken cancellationToken = default)
        {
            ReloadCount++;
            return Task.CompletedTask;
        }

        public IReadOnlyCollection<SkillDefinition> GetAll() => [];

        public SkillDefinition Get(string id) => throw new KeyNotFoundException(id);
    }
}
