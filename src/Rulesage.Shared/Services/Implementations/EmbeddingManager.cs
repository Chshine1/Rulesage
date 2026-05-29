using Npgsql;
using NpgsqlTypes;
using Pgvector;
using Rulesage.Shared.Services.Abstractions;
using Rulesage.Shared.Services.Abstractions.TextCleaner;

namespace Rulesage.Shared.Services.Implementations;

public class EmbeddingManager(NpgsqlDataSource dataSource, IDocumentSpaceProvider documentSpaceProvider, ITextCleaner textCleaner, IEmbeddingService embeddingService)
    : IEmbeddingManager
{
    public async Task GenerateEmbeddings(CancellationToken cancellationToken = default)
    {
        await using var conn = dataSource.CreateConnection();
        await conn.OpenAsync(cancellationToken);

        var communityData = await ReadAnnotationsAsync(conn, "communities", cancellationToken);
        var recordData = await ReadAnnotationsAsync(conn, "records", cancellationToken);
        var actionData = await ReadAnnotationsAsync(conn, "actions", cancellationToken);
        var ruleData = await ReadAnnotationsAsync(conn, "rules", cancellationToken);

        var allDocs = communityData.Select(x => x.Annotation)
            .Concat(recordData.Select(x => x.Annotation))
            .Concat(actionData.Select(x => x.Annotation))
            .Concat(ruleData.Select(x => x.Annotation))
            .ToArray();

        var documentSpace = documentSpaceProvider.CreateFromMemory(allDocs);
        var cleaned = textCleaner.Clean(documentSpace, allDocs);
        var embeddings = embeddingService.GetBatchEmbeddings(cleaned);

        var vectors = embeddings.Select(e => new Vector(e)).ToArray();

        var comCount = communityData.Count;
        var recCount = recordData.Count;
        var actCount = actionData.Count;

        var comVectors = vectors[..comCount];
        var recVectors = vectors[comCount..(comCount + recCount)];
        var actVectors = vectors[(comCount + recCount)..(comCount + recCount + actCount)];
        var ruleVectors = vectors[(comCount + recCount + actCount)..];

        await BulkUpdateEmbeddings(conn, "communities", communityData, comVectors, cancellationToken);
        await BulkUpdateEmbeddings(conn, "records", recordData, recVectors, cancellationToken);
        await BulkUpdateEmbeddings(conn, "actions", actionData, actVectors, cancellationToken);
        await BulkUpdateEmbeddings(conn, "rules", ruleData, ruleVectors, cancellationToken);
    }

    private static async Task<List<(string Id, string Annotation)>> ReadAnnotationsAsync(
        NpgsqlConnection conn, string tableName, CancellationToken ct)
    {
        var result = new List<(string, string)>();
        await using var cmd = new NpgsqlCommand($"SELECT id, annotation FROM {tableName} ORDER BY id", conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add((reader.GetString(0), reader.GetString(1)));
        }

        return result;
    }

    private static async Task BulkUpdateEmbeddings(
        NpgsqlConnection conn, string tableName,
        List<(string Id, string Annotation)> rows, Vector[] vectors, CancellationToken ct)
    {
        if (rows.Count == 0) return;

        var ids = rows.Select(r => r.Id).ToArray();

        await using var cmd = new NpgsqlCommand(
            $"""
            UPDATE {tableName} AS t
            SET annotation_embedding = v.emb
            FROM (SELECT * FROM unnest($1, $2)) AS v(id, emb)
            WHERE t.id = v.id
            """, conn);

        // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
        cmd.Parameters.Add(new NpgsqlParameter { Value = ids, NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text });
        cmd.Parameters.Add(new NpgsqlParameter { Value = vectors, DataTypeName = "vector[]" });

        await cmd.ExecuteNonQueryAsync(ct);
    }
}