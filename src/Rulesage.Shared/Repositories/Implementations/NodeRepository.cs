using System.Text.Json;
using Microsoft.FSharp.Collections;
using Npgsql;
using Rulesage.Common.Grammar.Ast;
using Rulesage.Shared.Repositories.Abstractions;

namespace Rulesage.Shared.Repositories.Implementations;

public class NodeRepository(NpgsqlDataSource dataSource, JsonSerializerOptions jsonOptions): INodeRepository
{
    public async Task<IEnumerable<string>> GetDocumentsAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = dataSource.CreateConnection();
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand("SELECT description FROM nodes", conn);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        return ReadToEnumerable(reader, r => r.GetString(0));
    }

    public async Task<IEnumerable<RecordExpr>> FindByIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        await using var conn = dataSource.CreateConnection();
        await conn.OpenAsync(cancellationToken);

        await using var cmd =
            new NpgsqlCommand(
                """
                SELECT
                    id,
                    description,
                    parameters
                FROM nodes
                WHERE id=ANY($1) 
                """,
                conn
            );
        
        cmd.Parameters.Add(ids.ToArray());
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        
        throw new NotImplementedException();
    }

    public async Task<IEnumerable<(RecordExpr, float)>> FindOrderByCosineDistanceAsync(double[] queryVector, int skip, int take,
        CancellationToken cancellationToken = default)
    {
        await using var conn = dataSource.CreateConnection();
        await conn.OpenAsync(cancellationToken);

        await using var cmd =
            new NpgsqlCommand(
                """
                SELECT
                    id,
                    description,
                    parameters,
                    (embedding <=> $1) AS distance
                FROM nodes
                ORDER BY embedding <=> $1
                LIMIT $2 OFFSET $3;
                """,
                conn
            );
        
        throw new NotImplementedException();
    }

    private static IEnumerable<T> ReadToEnumerable<T>(NpgsqlDataReader reader, Func<NpgsqlDataReader, T> func)
    {
        while (reader.Read()) yield return func(reader);
    }
}