using System.Text.Json;
using Microsoft.FSharp.Collections;
using Npgsql;
using NpgsqlTypes;
using Pgvector;
using Rulesage.Common.Grammar.Ast;
using Rulesage.Shared.Repositories.Abstractions;
using Rulesage.Shared.Services.Abstractions;

namespace Rulesage.Shared.Repositories.Implementations;

public class RecordRepository(NpgsqlDataSource dataSource,
    IEmbeddingService embeddingService, JsonSerializerOptions jsonOptions) : IRecordRepository
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
                JsonSerializer.Deserialize<FSharpList<string>>(genericParamsJson, jsonOptions);
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
                JsonSerializer.Deserialize<FSharpList<string>>(genericParamsJson, jsonOptions);
            var fors =
                JsonSerializer.Deserialize<FSharpMap<string, ParamExpr>>(forsJson, jsonOptions);

            return (
                new RecordExpr(id, community, annotation, genericParams, fors),
                (float)r.GetDouble(distanceOrdinal)
            );
        });
    }

    public async Task<bool> SaveAsync(IEnumerable<RecordExpr> records, CancellationToken cancellationToken = default)
    {
        await using var conn = dataSource.CreateConnection();
        await conn.OpenAsync(cancellationToken);

        var recordsArray = records.ToArray();
        var count = recordsArray.Length;

        var ids = new string[count];
        var communities = new string[count];
        var annotations = new string[count];
        var genericParams = new FSharpList<string>[count];
        var fors = new FSharpMap<string, ParamExpr>[count];

        for (var i = 0; i < count; i++)
        {
            var r = recordsArray[i];
            ids[i] = r.Id;
            communities[i] = r.Community;
            annotations[i] = r.Annotation;
            genericParams[i] = r.GenericParams;
            fors[i] = r.Fors;
        }

        var genericParamsJson = JsonSerializer.Serialize(genericParams, jsonOptions);
        var forsJson = JsonSerializer.Serialize(fors, jsonOptions);

        var embeddings = embeddingService
            .GetBatchEmbeddings(annotations)
            .Select(v => new Vector(v))
            .ToArray();

        await using var cmd =
            new NpgsqlCommand(
                """
                insert into records (id, community, annotation, generic_params, fors, annotation_embedding)
                select src.id, src.community, src.annotation, e1.generic_params, e2.fors, src.annotation_embedding 
                from unnest($1, $2, $3, $6) with ordinality as src(id, community, annotation, annotation_embedding, idx)
                join lateral jsonb_array_elements($4) with ordinality as e1(generic_params, idx1) on src.idx = idx1
                join lateral jsonb_array_elements($5) with ordinality as e2(fors, idx2) on src.idx = idx2
                """,
                conn
            );

        // ReSharper disable BitwiseOperatorOnEnumWithoutFlags
        cmd.Parameters.Add(new NpgsqlParameter { Value = ids, NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text });
        cmd.Parameters.Add(new NpgsqlParameter
            { Value = communities, NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text });
        cmd.Parameters.Add(new NpgsqlParameter
            { Value = annotations, NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text });
        // ReSharper restore BitwiseOperatorOnEnumWithoutFlags
        cmd.Parameters.Add(new NpgsqlParameter { Value = genericParamsJson, NpgsqlDbType = NpgsqlDbType.Jsonb });
        cmd.Parameters.Add(new NpgsqlParameter { Value = forsJson, NpgsqlDbType = NpgsqlDbType.Jsonb });
        cmd.Parameters.Add(new NpgsqlParameter { Value = embeddings, DataTypeName = "vector[]" });

        await cmd.ExecuteNonQueryAsync(cancellationToken);
        return true;
    }

    private static IEnumerable<T> ReadToEnumerable<T>(NpgsqlDataReader reader, Func<NpgsqlDataReader, T> func)
    {
        while (reader.Read()) yield return func(reader);
    }
}