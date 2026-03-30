using Microsoft.EntityFrameworkCore;

namespace ClawSharp.Lib.Runtime;

internal sealed class DelegatingDbContextFactory(DbContextOptions<ClawDbContext> options) : IDbContextFactory<ClawDbContext>
{
    public ClawDbContext CreateDbContext() => new(options);

    public Task<ClawDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CreateDbContext());
}