using ClawSharp.Lib.Configuration;

namespace ClawSharp.Lib.Runtime;

internal static class DatabasePathResolver
{
    public static string ResolveSqlitePath(ClawOptions options)
    {
        var configuredPath = string.IsNullOrWhiteSpace(options.Databases.Sqlite.DatabasePath)
            ? options.Sessions.DatabasePath
            : options.Databases.Sqlite.DatabasePath;
        return ResolvePath(options, configuredPath);
    }

    public static string ResolveDuckDbPath(ClawOptions options) =>
        ResolvePath(options, options.Databases.DuckDb.DatabasePath);

    private static string ResolvePath(ClawOptions options, string configuredPath)
    {
        var path = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(options.Runtime.WorkspaceRoot, configuredPath);
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        return fullPath;
    }
}
