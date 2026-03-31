using Microsoft.Data.Sqlite;
using ClawSharp.Lib.Configuration;

namespace ClawSharp.Lib.Runtime;

internal sealed record FeatureContext(
    string FeatureId,
    string CurrentPhase,
    bool IsScaffolded,
    string PlanChecksum,
    string? BranchName,
    string FeatureRootPath);

internal interface IFeatureContextRepository
{
    Task UpsertAsync(FeatureContext context, CancellationToken cancellationToken = default);

    Task<FeatureContext?> GetAsync(string featureId, CancellationToken cancellationToken = default);
}

internal sealed class FeatureContextRepository(ClawOptions options) : IFeatureContextRepository
{
    private readonly string _connectionString = $"Data Source={DatabasePathResolver.ResolveSqlitePath(options)}";
    private bool _initialized;
    private readonly Lock _sync = new();

    public async Task UpsertAsync(FeatureContext context, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO feature_contexts(feature_id, current_phase, is_scaffolded, plan_checksum, branch_name, feature_root_path)
            VALUES ($featureId, $currentPhase, $isScaffolded, $planChecksum, $branchName, $featureRootPath)
            ON CONFLICT(feature_id) DO UPDATE SET
                current_phase = excluded.current_phase,
                is_scaffolded = excluded.is_scaffolded,
                plan_checksum = excluded.plan_checksum,
                branch_name = excluded.branch_name,
                feature_root_path = excluded.feature_root_path;
            """;
        command.Parameters.AddWithValue("$featureId", context.FeatureId);
        command.Parameters.AddWithValue("$currentPhase", context.CurrentPhase);
        command.Parameters.AddWithValue("$isScaffolded", context.IsScaffolded ? 1 : 0);
        command.Parameters.AddWithValue("$planChecksum", context.PlanChecksum);
        command.Parameters.AddWithValue("$branchName", (object?)context.BranchName ?? DBNull.Value);
        command.Parameters.AddWithValue("$featureRootPath", context.FeatureRootPath);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<FeatureContext?> GetAsync(string featureId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT feature_id, current_phase, is_scaffolded, plan_checksum, branch_name, feature_root_path
            FROM feature_contexts
            WHERE feature_id = $featureId;
            """;
        command.Parameters.AddWithValue("$featureId", featureId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new FeatureContext(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetInt32(2) == 1,
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.GetString(5));
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        lock (_sync)
        {
            if (_initialized)
            {
                return;
            }
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS feature_contexts(
                feature_id TEXT PRIMARY KEY,
                current_phase TEXT NOT NULL,
                is_scaffolded INTEGER NOT NULL,
                plan_checksum TEXT NOT NULL,
                branch_name TEXT NULL,
                feature_root_path TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        lock (_sync)
        {
            _initialized = true;
        }
    }
}
