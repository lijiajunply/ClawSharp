namespace ClawSharp.Lib.Runtime;

internal interface IThreadSpaceRepository
{
    Task CreateAsync(ThreadSpaceRecord threadSpace, CancellationToken cancellationToken = default);

    Task<ThreadSpaceRecord?> GetAsync(ThreadSpaceId threadSpaceId, CancellationToken cancellationToken = default);

    Task<ThreadSpaceRecord?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<ThreadSpaceRecord?> GetByBoundFolderPathAsync(string boundFolderPath, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ThreadSpaceRecord>> ListAsync(bool includeArchived, CancellationToken cancellationToken = default);

    Task UpdateAsync(ThreadSpaceRecord threadSpace, CancellationToken cancellationToken = default);

    Task ArchiveAsync(ThreadSpaceId threadSpaceId, DateTimeOffset archivedAt, CancellationToken cancellationToken = default);
}
