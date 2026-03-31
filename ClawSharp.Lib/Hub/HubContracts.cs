using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Core;
using ClawSharp.Lib.Skills;

namespace ClawSharp.Lib.Hub;

/// <summary>
/// ClawHub 技能摘要。
/// </summary>
public sealed record HubSkillSummary(
    string Id,
    string Name,
    string Description,
    string LatestVersion,
    IReadOnlyList<string> Tags,
    int Downloads);

/// <summary>
/// ClawHub 技能详情。
/// </summary>
public sealed record HubSkillDetail(
    string Id,
    string Name,
    string Description,
    string LatestVersion,
    IReadOnlyList<string> Tags,
    int Downloads,
    IReadOnlyList<string> Versions,
    string? Readme,
    string? PackageUrl);

/// <summary>
/// ClawHub 技能包。
/// </summary>
public sealed record HubSkillPackage(
    string SkillId,
    string Version,
    string FileName,
    string ContentType,
    byte[] Content);

/// <summary>
/// 安装目标。
/// </summary>
public enum InstallTarget
{
    /// <summary>
    /// 安装到当前用户主目录下的 skills 目录。
    /// </summary>
    UserHome
}

/// <summary>
/// ClawHub 客户端抽象。
/// </summary>
public interface IHubClient
{
    /// <summary>
    /// 搜索 ClawHub 上可用的 skills。
    /// </summary>
    /// <param name="query">可选的搜索关键字；为空时返回默认结果。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>匹配的 skill 摘要列表。</returns>
    Task<IReadOnlyList<HubSkillSummary>> SearchSkillsAsync(string? query, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定 skill 的详细信息。
    /// </summary>
    /// <param name="skillId">skill 标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>skill 详细信息。</returns>
    Task<HubSkillDetail> GetSkillAsync(string skillId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 下载指定版本的 skill 安装包。
    /// </summary>
    /// <param name="skillId">skill 标识。</param>
    /// <param name="version">要下载的版本号。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>下载得到的安装包内容。</returns>
    Task<HubSkillPackage> DownloadSkillPackageAsync(string skillId, string version, CancellationToken cancellationToken = default);
}

/// <summary>
/// ClawHub 安装器抽象。
/// </summary>
public interface IHubInstaller
{
    /// <summary>
    /// 将 skill 安装包解压并安装到指定目标位置。
    /// </summary>
    /// <param name="package">待安装的安装包。</param>
    /// <param name="target">安装目标。</param>
    /// <param name="force">是否在已存在时强制覆盖。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>安装后的本地 skill 信息。</returns>
    Task<InstalledHubSkill> InstallAsync(HubSkillPackage package, InstallTarget target, bool force = false, CancellationToken cancellationToken = default);
}

/// <summary>
/// ClawHub 包校验器。
/// </summary>
public interface IHubManifestValidator
{
    /// <summary>
    /// 校验安装包文件并解析其中的 <c>SKILL.md</c> 定义。
    /// </summary>
    /// <param name="files">安装包展开后的文件集合。</param>
    /// <param name="packageFileName">安装包文件名。</param>
    /// <returns>解析出的 skill 定义。</returns>
    SkillDefinition Validate(IReadOnlyDictionary<string, byte[]> files, string packageFileName);
}

/// <summary>
/// 安装后的本地 skill 信息。
/// </summary>
public sealed record InstalledHubSkill(
    string SkillId,
    string Version,
    string InstallPath,
    SkillDefinition Definition);

/// <summary>
/// 默认 ClawHub 客户端。
/// </summary>
public sealed class HubClient : IHubClient
{
    private readonly HubOptions _options;
    private readonly Func<HttpClient> _httpClientFactory;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// 使用全局配置创建默认的 ClawHub 客户端。
    /// </summary>
    /// <param name="options">应用配置。</param>
    public HubClient(ClawOptions options)
        : this(options.Hub, () => CreateDefaultHttpClient(options.Hub))
    {
    }

    /// <summary>
    /// 使用指定 Hub 配置和 HTTP 客户端工厂创建 ClawHub 客户端。
    /// </summary>
    /// <param name="options">Hub 配置。</param>
    /// <param name="httpClientFactory">HTTP 客户端工厂。</param>
    public HubClient(HubOptions options, Func<HttpClient> httpClientFactory)
    {
        _options = options;
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HubSkillSummary>> SearchSkillsAsync(string? query, CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory();
        using var response = await client.GetAsync(BuildUri($"/api/v1/skills{BuildQuery(query)}"), cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var payload = await JsonSerializer.DeserializeAsync<List<HubSkillSummaryDto>>(stream, _jsonOptions, cancellationToken).ConfigureAwait(false);
        return payload?.Select(dto => dto.ToSummary()).ToArray() ?? [];
    }

    /// <inheritdoc />
    public async Task<HubSkillDetail> GetSkillAsync(string skillId, CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory();
        using var response = await client.GetAsync(BuildUri($"/api/v1/skills/{Uri.EscapeDataString(skillId)}"), cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var payload = await JsonSerializer.DeserializeAsync<HubSkillDetailDto>(stream, _jsonOptions, cancellationToken).ConfigureAwait(false)
                      ?? throw new ValidationException($"ClawHub returned an empty response for skill '{skillId}'.");
        return payload.ToDetail();
    }

    /// <inheritdoc />
    public async Task<HubSkillPackage> DownloadSkillPackageAsync(string skillId, string version, CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory();
        using var response = await client.GetAsync(BuildUri($"/api/v1/skills/{Uri.EscapeDataString(skillId)}/versions/{Uri.EscapeDataString(version)}/package"), cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var fileName = TryGetFileName(response.Content.Headers.ContentDisposition)
            ?? $"{skillId}-{version}.zip";
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        return new HubSkillPackage(skillId, version, fileName, contentType, bytes);
    }

    private Uri BuildUri(string relative) =>
        new(new Uri(NormalizeBaseUrl(_options.BaseUrl), UriKind.Absolute), relative.TrimStart('/'));

    private static string NormalizeBaseUrl(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new ValidationException("Hub:BaseUrl is required before using ClawHub commands.");
        }

        return baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/";
    }

    private static string BuildQuery(string? query) =>
        string.IsNullOrWhiteSpace(query) ? string.Empty : $"?q={Uri.EscapeDataString(query)}";

    private static HttpClient CreateDefaultHttpClient(HubOptions options)
    {
        return new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(Math.Max(options.TimeoutSeconds, 1))
        };
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var message = string.IsNullOrWhiteSpace(body)
            ? $"ClawHub request failed with status {(int)response.StatusCode} ({response.StatusCode})."
            : $"ClawHub request failed with status {(int)response.StatusCode} ({response.StatusCode}): {body}";

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new ValidationException(message);
        }

        throw new HttpRequestException(message, null, response.StatusCode);
    }

    private static string? TryGetFileName(ContentDispositionHeaderValue? contentDisposition)
    {
        var candidate = contentDisposition?.FileNameStar ?? contentDisposition?.FileName;
        return string.IsNullOrWhiteSpace(candidate) ? null : candidate.Trim('"');
    }

    private class HubSkillSummaryDto
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string LatestVersion { get; init; } = string.Empty;
        public List<string>? Tags { get; init; }
        public int Downloads { get; init; }

        public HubSkillSummary ToSummary() =>
            new(Id, Name, Description, LatestVersion, Tags ?? [], Downloads);
    }

    private sealed class HubSkillDetailDto : HubSkillSummaryDto
    {
        public List<string>? Versions { get; init; }
        public string? Readme { get; init; }
        public string? PackageUrl { get; init; }

        public HubSkillDetail ToDetail() =>
            new(Id, Name, Description, LatestVersion, Tags ?? [], Downloads, Versions ?? [], Readme, PackageUrl);
    }
}

/// <summary>
/// 默认包校验器。
/// </summary>
public sealed class HubManifestValidator : IHubManifestValidator
{
    private readonly MarkdownSkillParser _parser = new();

    /// <inheritdoc />
    public SkillDefinition Validate(IReadOnlyDictionary<string, byte[]> files, string packageFileName)
    {
        var skillEntry = files.FirstOrDefault(x => string.Equals(Path.GetFileName(x.Key), "SKILL.md", StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(skillEntry.Key))
        {
            throw new ValidationException($"Package '{packageFileName}' does not contain a SKILL.md file.");
        }

        var markdown = System.Text.Encoding.UTF8.GetString(skillEntry.Value).TrimStart('\uFEFF');
        return _parser.Parse(markdown, sourcePath: skillEntry.Key);
    }
}

/// <summary>
/// 默认安装器。
/// </summary>
public sealed class HubInstaller : IHubInstaller
{
    private readonly HubOptions _options;
    private readonly IHubManifestValidator _validator;
    private readonly ISkillRegistry _skills;

    /// <summary>
    /// 使用全局配置、包校验器和 skill 注册表创建安装器。
    /// </summary>
    /// <param name="options">应用配置。</param>
    /// <param name="validator">安装包校验器。</param>
    /// <param name="skills">skill 注册表。</param>
    public HubInstaller(ClawOptions options, IHubManifestValidator validator, ISkillRegistry skills)
    {
        _options = options.Hub;
        _validator = validator;
        _skills = skills;
    }

    /// <inheritdoc />
    public async Task<InstalledHubSkill> InstallAsync(HubSkillPackage package, InstallTarget target, bool force = false, CancellationToken cancellationToken = default)
    {
        var files = ExtractFiles(package);
        var definition = _validator.Validate(files, package.FileName);
        var installRoot = ResolveInstallRoot(_options, target);
        Directory.CreateDirectory(installRoot);

        var targetDirectory = Path.Combine(installRoot, definition.Id);
        if (Directory.Exists(targetDirectory))
        {
            if (!force)
            {
                throw new ValidationException($"Skill '{definition.Id}' is already installed at '{targetDirectory}'. Use --force to replace it.");
            }

            Directory.Delete(targetDirectory, recursive: true);
        }

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = NormalizeRelativePath(file.Key);
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                continue;
            }

            var destinationPath = Path.Combine(targetDirectory, relativePath);
            var destinationDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            await File.WriteAllBytesAsync(destinationPath, file.Value, cancellationToken).ConfigureAwait(false);
        }

        await _skills.ReloadAsync(cancellationToken).ConfigureAwait(false);
        return new InstalledHubSkill(definition.Id, package.Version, targetDirectory, definition);
    }

    internal static string ResolveInstallRoot(HubOptions options, InstallTarget target)
    {
        if (target != InstallTarget.UserHome)
        {
            throw new ValidationException($"Unsupported install target '{target}'.");
        }

        var configured = string.IsNullOrWhiteSpace(options.InstallRoot) ? "~/.skills" : options.InstallRoot;
        if (configured == "~")
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        if (configured.StartsWith("~/", StringComparison.Ordinal))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.GetFullPath(Path.Combine(home, configured[2..]));
        }

        return Path.GetFullPath(configured);
    }

    internal static IReadOnlyDictionary<string, byte[]> ExtractFiles(HubSkillPackage package)
    {
        var extension = package.FileName.ToLowerInvariant();
        return extension switch
        {
            var x when x.EndsWith(".zip", StringComparison.Ordinal) => ExtractZip(package.Content),
            var x when x.EndsWith(".tar.gz", StringComparison.Ordinal) || x.EndsWith(".tgz", StringComparison.Ordinal) => ExtractTarGz(package.Content),
            var x when x.EndsWith(".tar", StringComparison.Ordinal) => ExtractTar(package.Content),
            _ => throw new ValidationException($"Unsupported package format '{package.FileName}'. Expected .zip, .tar, or .tar.gz.")
        };
    }

    private static IReadOnlyDictionary<string, byte[]> ExtractZip(byte[] bytes)
    {
        var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        using var memory = new MemoryStream(bytes);
        using var archive = new ZipArchive(memory, ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.EndsWith("/"))
            {
                continue;
            }

            using var entryStream = entry.Open();
            using var copy = new MemoryStream();
            entryStream.CopyTo(copy);
            files[NormalizeRelativePath(entry.FullName)] = copy.ToArray();
        }

        return files;
    }

    private static IReadOnlyDictionary<string, byte[]> ExtractTarGz(byte[] bytes)
    {
        using var compressed = new MemoryStream(bytes);
        using var gzip = new System.IO.Compression.GZipStream(compressed, CompressionMode.Decompress);
        using var expanded = new MemoryStream();
        gzip.CopyTo(expanded);
        return ExtractTar(expanded.ToArray());
    }

    private static IReadOnlyDictionary<string, byte[]> ExtractTar(byte[] bytes)
    {
        var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        using var memory = new MemoryStream(bytes);
        using var reader = new System.Formats.Tar.TarReader(memory, leaveOpen: false);
        System.Formats.Tar.TarEntry? entry;
        while ((entry = reader.GetNextEntry()) != null)
        {
            if (entry.EntryType is System.Formats.Tar.TarEntryType.Directory)
            {
                continue;
            }

            using var copy = new MemoryStream();
            entry.DataStream?.CopyTo(copy);
            files[NormalizeRelativePath(entry.Name)] = copy.ToArray();
        }

        return files;
    }

    internal static string NormalizeRelativePath(string path)
    {
        var normalized = path.Replace('\\', '/').Trim('/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment == ".."))
        {
            throw new ValidationException($"Package contains an unsafe path '{path}'.");
        }

        if (segments.Length > 1 && !IsSkillRootFile(segments[0]) && !segments[0].Contains('.'))
        {
            return string.Join(Path.DirectorySeparatorChar, segments[1..]);
        }

        return string.Join(Path.DirectorySeparatorChar, segments);
    }

    private static bool IsSkillRootFile(string segment) =>
        string.Equals(segment, "SKILL.md", StringComparison.OrdinalIgnoreCase)
        || string.Equals(segment, "assets", StringComparison.OrdinalIgnoreCase)
        || string.Equals(segment, "scripts", StringComparison.OrdinalIgnoreCase);
}
