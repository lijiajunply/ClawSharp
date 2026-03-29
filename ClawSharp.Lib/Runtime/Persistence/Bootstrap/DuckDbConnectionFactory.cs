using ClawSharp.Lib.Configuration;
using DuckDB.NET.Data;

namespace ClawSharp.Lib.Runtime;

internal sealed class DuckDbConnectionFactory(ClawOptions options)
{
    private string DatabasePath { get; } = DatabasePathResolver.ResolveDuckDbPath(options);

    public DuckDBConnection Open()
    {
        var connection = new DuckDBConnection($"DataSource={DatabasePath}");
        connection.Open();
        return connection;
    }
}
