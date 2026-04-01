using System.Text.Json;
using ClawSharp.Lib.Configuration;
using Microsoft.Data.Sqlite;

namespace ClawSharp.Lib.Memory;

/// <summary>
/// 基于 SQLite-VSS 的持久化向量存储实现。
/// </summary>
public sealed class SqliteVssVectorStore : IVectorStore, IDisposable
{
    private readonly ClawOptions _options;
    private readonly SqliteConnection _connection;
    private bool _initialized;

    /// <summary>
    /// 创建一个基于 SQLite-VSS 的持久化向量存储实现。
    /// </summary>
    /// <param name="options">配置选项</param>
    public SqliteVssVectorStore(ClawOptions options)
    {
        _options = options;
        var dbPath = string.IsNullOrEmpty(options.Databases.Sqlite.DatabasePath)
            ? Path.Combine(options.Runtime.WorkspaceRoot, options.Runtime.DataPath, "clawsharp.db")
            : options.Databases.Sqlite.DatabasePath;

        if (!Path.IsPathRooted(dbPath))
        {
            dbPath = Path.GetFullPath(Path.Combine(options.Runtime.WorkspaceRoot, dbPath));
        }

        var directory = Path.GetDirectoryName(dbPath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connection = new SqliteConnection($"Data Source={dbPath}");
    }

    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (_initialized) return;

        await _connection.OpenAsync(ct);

        // 加载 vss 扩展
        try
        {
            // vector0 必须在 vss0 之前加载
            LoadNativeExtension("vector0");
            LoadNativeExtension("vss0");
        }
        catch (SqliteException ex)
        {
            Console.WriteLine($"Warning: Failed to load sqlite-vss extension: {ex.Message}");
        }

        // 初始化表
        using var command = _connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS chunk_metadata (
                rowid INTEGER PRIMARY KEY,
                id TEXT NOT NULL,
                document_id TEXT NOT NULL,
                content TEXT NOT NULL,
                metadata TEXT,
                scope TEXT NOT NULL
            );

            CREATE VIRTUAL TABLE IF NOT EXISTS vss_chunks USING vss0(
                embedding(" + _options.Embedding.Dimensions + @")
            );
        ";
        await command.ExecuteNonQueryAsync(ct);

        _initialized = true;
    }

    private void LoadNativeExtension(string name)
    {
        var baseDir = AppContext.BaseDirectory;
        var ext = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows) ? ".dll" :
                  System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX) ? ".dylib" : ".so";

        // 尝试在当前执行目录加载
        var probePath = Path.Combine(baseDir, name + ext);
        if (File.Exists(probePath))
        {
            _connection.LoadExtension(probePath);
            return;
        }

        // 回退到系统路径加载
        _connection.LoadExtension(name);
    }

    /// <summary>
    /// 批量插入或更新内存块。
    /// </summary>
    /// <param name="chunks">内存块列表</param>
    /// <param name="embeddings">嵌入向量列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Task</returns>
    public async Task UpsertAsync(IReadOnlyList<MemoryChunk> chunks, IReadOnlyList<EmbeddingVector> embeddings, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        using var transaction = _connection.BeginTransaction();
        try
        {
            for (int i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                var vector = embeddings[i];

                // 1. 插入或更新元数据
                using var metaCmd = _connection.CreateCommand();
                metaCmd.Transaction = transaction;
                metaCmd.CommandText = @"
                    INSERT INTO chunk_metadata (id, document_id, content, metadata, scope)
                    VALUES (@id, @docId, @content, @meta, @scope)
                    RETURNING rowid;
                ";
                metaCmd.Parameters.AddWithValue("@id", chunk.Id);
                metaCmd.Parameters.AddWithValue("@docId", chunk.DocumentId);
                metaCmd.Parameters.AddWithValue("@content", chunk.Content);
                metaCmd.Parameters.AddWithValue("@meta", chunk.Metadata != null ? JsonSerializer.Serialize(chunk.Metadata) : (object)DBNull.Value);
                metaCmd.Parameters.AddWithValue("@scope", chunk.Scope);

                var rowId = await metaCmd.ExecuteScalarAsync(cancellationToken);

                // 2. 插入向量
                using var vssCmd = _connection.CreateCommand();
                vssCmd.Transaction = transaction;
                vssCmd.CommandText = "INSERT INTO vss_chunks (rowid, embedding) VALUES (@rowid, @vector)";
                vssCmd.Parameters.AddWithValue("@rowid", rowId);
                vssCmd.Parameters.AddWithValue("@vector", JsonSerializer.Serialize(vector.Values));
                await vssCmd.ExecuteNonQueryAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// 根据作用域删除内存块。
    /// </summary>
    /// <param name="scope">作用域</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Task</returns>
    public async Task DeleteByScopeAsync(string scope, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        using var transaction = _connection.BeginTransaction();
        try
        {
            // 找到对应的 rowid
            var rowIds = new List<long>();
            using (var selectCmd = _connection.CreateCommand())
            {
                selectCmd.Transaction = transaction;
                selectCmd.CommandText = "SELECT rowid FROM chunk_metadata WHERE scope = @scope";
                selectCmd.Parameters.AddWithValue("@scope", scope);
                using var reader = await selectCmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    rowIds.Add(reader.GetInt64(0));
                }
            }

            if (rowIds.Count > 0)
            {
                var idsStr = string.Join(",", rowIds);

                using (var delVss = _connection.CreateCommand())
                {
                    delVss.Transaction = transaction;
                    delVss.CommandText = $"DELETE FROM vss_chunks WHERE rowid IN ({idsStr})";
                    await delVss.ExecuteNonQueryAsync(cancellationToken);
                }

                using (var delMeta = _connection.CreateCommand())
                {
                    delMeta.Transaction = transaction;
                    delMeta.CommandText = "DELETE FROM chunk_metadata WHERE scope = @scope";
                    delMeta.Parameters.AddWithValue("@scope", scope);
                    await delMeta.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// 根据查询参数进行查询。
    /// </summary>
    /// <param name="query">查询参数</param>
    /// <param name="embedding">嵌入向量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>查询结果列表</returns>
    public async Task<IReadOnlyList<MemorySearchResult>> QueryAsync(MemoryQuery query, EmbeddingVector embedding, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var availableCount = await CountScopeEntriesAsync(query.Scope, cancellationToken);
        if (availableCount == 0)
        {
            return [];
        }

        var topK = NormalizeTopK(query.TopK);
        topK = Math.Min(topK, availableCount);
        var results = new List<MemorySearchResult>();

        using var command = _connection.CreateCommand();
        // 使用 vss_search 进行检索
        command.CommandText = @"
            SELECT m.id, m.document_id, m.content, v.distance, m.metadata
            FROM vss_chunks v
            JOIN chunk_metadata m ON v.rowid = m.rowid
            WHERE m.scope = @scope
              AND vss_search(v.embedding, vss_search_params(@query, @topk))
            ORDER BY v.distance ASC;
        ";
        command.Parameters.AddWithValue("@scope", query.Scope);
        command.Parameters.AddWithValue("@query", JsonSerializer.Serialize(embedding.Values));
        command.Parameters.AddWithValue("@topk", topK);

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetString(0);
            var docId = reader.GetString(1);
            var content = reader.GetString(2);
            var distance = reader.GetDouble(3);
            var metaJson = reader.IsDBNull(4) ? null : reader.GetString(4);
            var metadata = metaJson != null ? JsonSerializer.Deserialize<Dictionary<string, string>>(metaJson) : null;

            // 注意：VSS 返回的是距离，距离越小相关性越高。Score 通常定义为相关性（越大越好）。
            // 简单转换：1 / (1 + distance)
            results.Add(new MemorySearchResult(id, docId, content, 1.0 / (1.0 + distance), metadata));
        }

        return results;
    }

    private async Task<int> CountScopeEntriesAsync(string scope, CancellationToken cancellationToken)
    {
        using var countCommand = _connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(1) FROM chunk_metadata WHERE scope = @scope";
        countCommand.Parameters.AddWithValue("@scope", scope);
        var count = await countCommand.ExecuteScalarAsync(cancellationToken);
        return count is null or DBNull ? 0 : Convert.ToInt32(count);
    }

    internal static int NormalizeTopK(int topK) => topK > 0 ? topK : 1;

    /// <summary>
    /// 释放资源，关闭数据库连接。
    /// </summary>
    public void Dispose()
    {
        _connection.Dispose();
    }
}
