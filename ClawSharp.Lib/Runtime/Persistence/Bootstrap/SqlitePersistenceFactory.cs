using ClawSharp.Lib.Configuration;
using Microsoft.EntityFrameworkCore;

namespace ClawSharp.Lib.Runtime;

internal static class SqlitePersistenceFactory
{
    public static IDbContextFactory<ClawDbContext> CreateDbContextFactory(ClawOptions options)
    {
        var builder = new DbContextOptionsBuilder<ClawDbContext>();
        builder.UseSqlite(
            $"Data Source={DatabasePathResolver.ResolveSqlitePath(options)}",
            sqlite => sqlite.MigrationsAssembly(typeof(ClawDbContext).Assembly.FullName));
        return new DelegatingDbContextFactory(builder.Options);
    }

    public static ClawSqliteDatabaseInitializer CreateInitializer(ClawOptions options, IDbContextFactory<ClawDbContext>? dbContextFactory = null) =>
        new(dbContextFactory ?? CreateDbContextFactory(options), options);

    public static IThreadSpaceRepository CreateThreadSpaceRepository(ClawOptions options)
    {
        var dbContextFactory = CreateDbContextFactory(options);
        return new EfThreadSpaceRepository(dbContextFactory, CreateInitializer(options, dbContextFactory));
    }

    public static ISessionRecordRepository CreateSessionRecordRepository(ClawOptions options)
    {
        var dbContextFactory = CreateDbContextFactory(options);
        return new EfSessionRecordRepository(dbContextFactory, CreateInitializer(options, dbContextFactory));
    }

    public static IPromptMessageRepository CreatePromptMessageRepository(ClawOptions options)
    {
        var dbContextFactory = CreateDbContextFactory(options);
        return new EfPromptMessageRepository(dbContextFactory, CreateInitializer(options, dbContextFactory));
    }

    public static ISessionEventRepository CreateSessionEventRepository(ClawOptions options)
    {
        var dbContextFactory = CreateDbContextFactory(options);
        return new EfSessionEventRepository(dbContextFactory, CreateInitializer(options, dbContextFactory));
    }
}