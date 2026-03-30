using System.Collections.Concurrent;
using ClawSharp.Lib.Configuration;
using Microsoft.EntityFrameworkCore;

namespace ClawSharp.Lib.Runtime;

internal sealed class ClawSqliteDatabaseInitializer(
    IDbContextFactory<ClawDbContext> dbContextFactory,
    ClawOptions options)
{
    private const string GlobalThreadSpaceName = "global";
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
        var existing = context.ThreadSpaces.SingleOrDefault(x => x.IsGlobal);
        if (existing is null)
        {
            // Fallback for very old databases that might not have is_global correctly set but have name="init"
            existing = context.ThreadSpaces.SingleOrDefault(x => x.Name == GlobalThreadSpaceName || x.Name == "init");
        }

        if (existing is null)
        {
            context.ThreadSpaces.Add(new ThreadSpaceEntity
            {
                ThreadSpaceId = ThreadSpaceId.New().Value,
                Name = GlobalThreadSpaceName,
                BoundFolderPath = null,
                IsGlobal = true,
                CreatedAt = DateTimeOffset.UtcNow,
                ArchivedAt = null
            });
            context.SaveChanges();
            return;
        }

        // Migrate existing global/init space to new naming and null path
        var changed = false;
        if (existing.Name != GlobalThreadSpaceName)
        {
            existing.Name = GlobalThreadSpaceName;
            changed = true;
        }
        if (!existing.IsGlobal)
        {
            existing.IsGlobal = true;
            changed = true;
        }
        if (existing.BoundFolderPath != null)
        {
            existing.BoundFolderPath = null;
            changed = true;
        }

        if (changed)
        {
            context.SaveChanges();
        }
    }
}