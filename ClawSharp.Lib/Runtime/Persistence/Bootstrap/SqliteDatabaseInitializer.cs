using System.Collections.Concurrent;
using ClawSharp.Lib.Configuration;
using Microsoft.EntityFrameworkCore;

namespace ClawSharp.Lib.Runtime;

internal sealed class ClawSqliteDatabaseInitializer(
    IDbContextFactory<ClawDbContext> dbContextFactory,
    ClawOptions options)
{
    private const string InitThreadSpaceName = "init";
    private static readonly ConcurrentDictionary<string, object> InitializationGates = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, bool> InitializedDatabases = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _databasePath = DatabasePathResolver.ResolveSqlitePath(options);
    private volatile bool _initialized;

    public void EnsureInitialized()
    {
        if ((_initialized || InitializedDatabases.ContainsKey(_databasePath)) && File.Exists(_databasePath))
        {
            _initialized = true;
            return;
        }

        var gate = InitializationGates.GetOrAdd(_databasePath, static _ => new object());
        lock (gate)
        {
            if ((_initialized || InitializedDatabases.ContainsKey(_databasePath)) && File.Exists(_databasePath))
            {
                _initialized = true;
                return;
            }

            using var context = dbContextFactory.CreateDbContext();
            context.Database.Migrate();
            EnsureInitThreadSpace(context);
            InitializedDatabases[_databasePath] = true;
            _initialized = true;
        }
    }

    private void EnsureInitThreadSpace(ClawDbContext context)
    {
        var workspaceRoot = Path.GetFullPath(options.Runtime.WorkspaceRoot);
        Directory.CreateDirectory(workspaceRoot);

        var existing = context.ThreadSpaces.SingleOrDefault(x => x.Name == InitThreadSpaceName);
        if (existing is null)
        {
            context.ThreadSpaces.Add(new ThreadSpaceEntity
            {
                ThreadSpaceId = ThreadSpaceId.New().Value,
                Name = InitThreadSpaceName,
                BoundFolderPath = workspaceRoot,
                IsInit = true,
                CreatedAt = DateTimeOffset.UtcNow,
                ArchivedAt = null
            });
            context.SaveChanges();
            return;
        }

        if (existing.BoundFolderPath == workspaceRoot && existing.IsInit)
        {
            return;
        }

        existing.BoundFolderPath = workspaceRoot;
        existing.IsInit = true;
        context.SaveChanges();
    }
}
