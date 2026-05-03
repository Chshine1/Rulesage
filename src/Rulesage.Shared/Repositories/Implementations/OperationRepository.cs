using System.Text.Json;
using Microsoft.FSharp.Collections;
using Npgsql;
using Rulesage.Common.Types.Domain;
using Rulesage.Shared.Repositories.Abstractions;

namespace Rulesage.Shared.Repositories.Implementations;

public class OperationRepository(NpgsqlDataSource dataSource, JsonSerializerOptions jsonOptions) : IOperationRepository
{
    public async Task<IEnumerable<string>> GetDocumentsAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = dataSource.CreateConnection();
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand("SELECT description FROM operations", conn);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        return ReadToEnumerable(reader, r => r.GetString(0));
    }

    public async Task<OperationBlueprint?> FindByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var conn = dataSource.CreateConnection();
        await conn.OpenAsync(cancellationToken);

        await using var cmd =
            new NpgsqlCommand("SELECT subtasks, outputs FROM operations WHERE id = $1", conn);
        cmd.Parameters.Add(id);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken)) return null;

        var subtasksJson = reader.GetString(0);
        var outputsJson = reader.GetString(1);

        var subtasks =
            JsonSerializer.Deserialize<FSharpMap<string, Subtask>>(subtasksJson, jsonOptions);

        var outputs =
            JsonSerializer.Deserialize<FSharpMap<string, NodeBlueprint>>(outputsJson, jsonOptions);

        return new OperationBlueprint(subtasks, outputs);
    }

    public async Task<IEnumerable<(OperationSignature, float)>> FindOrderByCosineDistanceAsync(float[] queryVector,
        int skip, int take,
        CancellationToken cancellationToken = default)
    {
        await using var conn = dataSource.CreateConnection();
        await conn.OpenAsync(cancellationToken);

        await using var cmd =
            new NpgsqlCommand(
                """
                SELECT
                    id,
                    ir,
                    description,
                    level,
                    signature_params,
                    signature_outputs,
                    (embedding <=> $1) AS distance
                FROM operations
                ORDER BY embedding <=> $1
                LIMIT $2 OFFSET $3;
                """,
                conn
            );

        cmd.Parameters.Add(queryVector);
        cmd.Parameters.Add(take);
        cmd.Parameters.Add(skip);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        return ReadToEnumerable(reader, r =>
        {
            var parameters =
                JsonSerializer.Deserialize<FSharpMap<string, ParamType>>(r.GetString(4), jsonOptions);

            var outputs =
                JsonSerializer.Deserialize<FSharpMap<string, ParamType>>(r.GetString(5), jsonOptions);

            return (
                new OperationSignature(
                    new Identifier(r.GetInt32(0), r.GetString(1)),
                    r.GetString(2),
                    r.GetFloat(3),
                    parameters,
                    outputs
                ),
                (float)r.GetDouble(6)
            );
        });
    }

    private static IEnumerable<T> ReadToEnumerable<T>(NpgsqlDataReader reader, Func<NpgsqlDataReader, T> func)
    {
        while (reader.Read()) yield return func(reader);
    }
}