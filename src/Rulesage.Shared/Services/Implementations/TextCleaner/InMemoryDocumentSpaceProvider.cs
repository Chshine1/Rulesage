using Npgsql;
using Rulesage.Shared.Services.Abstractions.TextCleaner;

namespace Rulesage.Shared.Services.Implementations.TextCleaner;

public class InMemoryDocumentSpaceProvider(NpgsqlDataSource dataSource) : IDocumentSpaceProvider
{
    private IDocumentSpace? _cachedSpace;
    private readonly SemaphoreSlim _lock = new(1, 1);

    // TODO: Dependency management
    public IDocumentSpace CreateFromMemory(IReadOnlyList<string> documents)
    {
        return new InMemoryDocumentSpace(documents);
    }

    public async ValueTask<IDocumentSpace> GetDocumentSpaceFromDbAsync(
        CancellationToken ct = default)
    {
        if (_cachedSpace is not null)
            return _cachedSpace;

        await _lock.WaitAsync(ct);
        try
        {
            if (_cachedSpace is not null)
                return _cachedSpace;

            var tasks = new[]
            {
                ReadAnnotationsAsync("communities", ct),
                ReadAnnotationsAsync("records", ct),
                ReadAnnotationsAsync("actions", ct),
                ReadAnnotationsAsync("rules", ct)
            };
            var results = await Task.WhenAll(tasks);
            var allAnnotations = results.SelectMany(x => x).ToList();
            _cachedSpace = new InMemoryDocumentSpace(allAnnotations);
            return _cachedSpace;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<string>> ReadAnnotationsAsync(string tableName, CancellationToken ct)
    {
        await using var conn = dataSource.CreateConnection();
        await conn.OpenAsync(ct);

        var annotations = new List<string>();
        await using var cmd = new NpgsqlCommand(
            $"SELECT annotation FROM {tableName} ORDER BY id", conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            annotations.Add(reader.GetString(0));
        return annotations;
    }
}