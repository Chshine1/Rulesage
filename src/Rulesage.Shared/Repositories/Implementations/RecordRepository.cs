using System.Text.Json;
using Microsoft.FSharp.Collections;
using Npgsql;
using NpgsqlTypes;
using Pgvector;
using Rulesage.Common.Grammar.Ast;
using Rulesage.Shared.Repositories.Abstractions;

namespace Rulesage.Shared.Repositories.Implementations;

public class RecordRepository(NpgsqlDataSource dataSource, JsonSerializerOptions jsonOptions) : IRecordRepository
{
    public async Task<IEnumerable<string>> GetDocumentsAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = dataSource.CreateConnection();
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand("SELECT annotation FROM records", conn);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        return ReadToEnumerable(reader, r => r.GetString(0));
    }

    public async Task<IEnumerable<RecordExpr>> FindByIdsAsync(IEnumerable<string> ids,
        CancellationToken cancellationToken = default)
    {
        await using var conn = dataSource.CreateConnection();
        await conn.OpenAsync(cancellationToken);

        await using var cmd =
            new NpgsqlCommand(
                """
                select
                    id,
                    community,
                    annotation,
                    generic_params,
                    fors
                from records
                where id=any($1) 
                """,
                conn
            );

        // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
        cmd.Parameters.Add(new NpgsqlParameter<string[]>
            { Value = ids.ToArray(), NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text });

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var idOrdinal = reader.GetOrdinal("id");
        var communityOrdinal = reader.GetOrdinal("community");
        var annotationOrdinal = reader.GetOrdinal("annotation");
        var genericParamsOrdinal = reader.GetOrdinal("generic_params");
        var forsOrdinal = reader.GetOrdinal("fors");

        return ReadToEnumerable(reader, r =>
        {
            var id = r.GetString(idOrdinal);
            var community = r.GetString(communityOrdinal);
            var annotation = r.GetString(annotationOrdinal);
            var genericParamsJson = r.GetString(genericParamsOrdinal);
            var forsJson = r.GetString(forsOrdinal);

            var genericParams =
                JsonSerializer.Deserialize<string[]>(genericParamsJson, jsonOptions);
            var fors =
                JsonSerializer.Deserialize<FSharpMap<string, ParamExpr>>(forsJson, jsonOptions);

            return new RecordExpr(id, community, annotation, genericParams, fors);
        });
    }

    public async Task<IEnumerable<(RecordExpr, float)>> FindOrderByCosineDistanceAsync(float[] queryVector, int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        await using var conn = dataSource.CreateConnection();
        await conn.OpenAsync(cancellationToken);

        await using var cmd =
            new NpgsqlCommand(
                """
                select
                    id,
                    community,
                    annotation,
                    generic_params,
                    fors,
                    (annotation_embedding <=> $1) as distance
                from records
                order by distance
                limit $2 offset $3;
                """,
                conn
            );

        cmd.Parameters.Add(new NpgsqlParameter
            { Value = new Vector(queryVector), NpgsqlDbType = NpgsqlDbType.Unknown });
        cmd.Parameters.Add(new NpgsqlParameter<int> { Value = take, NpgsqlDbType = NpgsqlDbType.Integer });
        cmd.Parameters.Add(new NpgsqlParameter<int> { Value = skip, NpgsqlDbType = NpgsqlDbType.Integer });

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var idOrdinal = reader.GetOrdinal("id");
        var communityOrdinal = reader.GetOrdinal("community");
        var annotationOrdinal = reader.GetOrdinal("annotation");
        var genericParamsOrdinal = reader.GetOrdinal("generic_params");
        var forsOrdinal = reader.GetOrdinal("fors");
        var distanceOrdinal = reader.GetOrdinal("distance");

        return ReadToEnumerable(reader, r =>
        {
            var id = r.GetString(idOrdinal);
            var community = r.GetString(communityOrdinal);
            var annotation = r.GetString(annotationOrdinal);
            var genericParamsJson = r.GetString(genericParamsOrdinal);
            var forsJson = r.GetString(forsOrdinal);

            var genericParams =
                JsonSerializer.Deserialize<string[]>(genericParamsJson, jsonOptions);
            var fors =
                JsonSerializer.Deserialize<FSharpMap<string, ParamExpr>>(forsJson, jsonOptions);

            return (
                new RecordExpr(id, community, annotation, genericParams, fors),
                (float)r.GetDouble(distanceOrdinal)
            );
        });
    }

    private static IEnumerable<T> ReadToEnumerable<T>(NpgsqlDataReader reader, Func<NpgsqlDataReader, T> func)
    {
        while (reader.Read()) yield return func(reader);
    }
}