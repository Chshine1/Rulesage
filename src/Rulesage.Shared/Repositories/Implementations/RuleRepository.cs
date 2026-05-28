using System.Text.Json;
using Microsoft.FSharp.Collections;
using Npgsql;
using NpgsqlTypes;
using Pgvector;
using Rulesage.Common.Grammar.Ast;
using Rulesage.Shared.Repositories.Abstractions;
using Rulesage.Shared.Services.Abstractions;

namespace Rulesage.Shared.Repositories.Implementations;

public class RuleRepository(
    NpgsqlDataSource dataSource,
    IEmbeddingService embeddingService,
    JsonSerializerOptions jsonOptions) : IRuleRepository
{
    public async Task<IEnumerable<string>> GetDocumentsAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = dataSource.CreateConnection();
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand("SELECT annotation FROM rules", conn);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        return ReadToEnumerable(reader, r => r.GetString(0));
    }

    public async Task<IEnumerable<RuleExpr>> FindByIdsAsync(IEnumerable<string> ids,
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
                    fors,
                    givens,
                    must_be
                from rules
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
        var forsOrdinal = reader.GetOrdinal("fors");
        var givensOrdinal = reader.GetOrdinal("givens");
        var mustBeOrdinal = reader.GetOrdinal("must_be");

        return ReadToEnumerable(reader, r =>
        {
            var id = r.GetString(idOrdinal);
            var community = r.GetString(communityOrdinal);
            var annotation = r.GetString(annotationOrdinal);
            var forsJson = r.GetString(forsOrdinal);
            var givensJson = r.GetString(givensOrdinal);
            var mustBeJson = r.GetString(mustBeOrdinal);

            var fors =
                JsonSerializer.Deserialize<FSharpMap<string, ParamExpr>>(forsJson, jsonOptions);
            var givens =
                JsonSerializer.Deserialize<FSharpMap<string, GivenExpr>>(givensJson, jsonOptions);
            var mustBe = JsonSerializer.Deserialize<ValueExpr>(mustBeJson, jsonOptions);

            return new RuleExpr(id, community, annotation, fors, givens, mustBe);
        });
    }

    public async Task<IEnumerable<(RuleExpr, float)>> FindOrderByCosineDistanceAsync(float[] queryVector,
        int skip, int take,
        CancellationToken cancellationToken = default)
    {
        await using var conn = dataSource.CreateConnection();
        await conn.OpenAsync(cancellationToken);

        await using var cmd =
            new NpgsqlCommand(
                """
                select
                    id,
                    annotation,
                    fors,
                    givens,
                    must_be,
                    (annotation_embedding <=> $1) as distance
                from rules
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
        var forsOrdinal = reader.GetOrdinal("fors");
        var givensOrdinal = reader.GetOrdinal("givens");
        var mustBeOrdinal = reader.GetOrdinal("must_be");
        var distanceOrdinal = reader.GetOrdinal("distance");

        return ReadToEnumerable(reader, r =>
        {
            var id = r.GetString(idOrdinal);
            var community = r.GetString(communityOrdinal);
            var annotation = r.GetString(annotationOrdinal);
            var forsJson = r.GetString(forsOrdinal);
            var givensJson = r.GetString(givensOrdinal);
            var mustBeJson = r.GetString(mustBeOrdinal);

            var fors =
                JsonSerializer.Deserialize<FSharpMap<string, ParamExpr>>(forsJson, jsonOptions);
            var givens =
                JsonSerializer.Deserialize<FSharpMap<string, GivenExpr>>(givensJson, jsonOptions);
            var mustBe = JsonSerializer.Deserialize<ValueExpr>(mustBeJson, jsonOptions);

            return (
                new RuleExpr(id, community, annotation, fors, givens, mustBe),
                (float)r.GetDouble(distanceOrdinal)
            );
        });
    }

    public async Task<bool> SaveAsync(IEnumerable<RuleExpr> rules, CancellationToken cancellationToken = default)
    {
        await using var conn = dataSource.CreateConnection();
        await conn.OpenAsync(cancellationToken);

        var rulesArray = rules.ToArray();
        var count = rulesArray.Length;

        var ids = new string[count];
        var communities = new string[count];
        var annotations = new string[count];
        var fors = new FSharpMap<string, ParamExpr>[count];
        var givens = new FSharpMap<string, GivenExpr>[count];
        var mustBes = new ValueExpr[count];

        for (var i = 0; i < count; i++)
        {
            var r = rulesArray[i];
            ids[i] = r.Id;
            communities[i] = r.Community;
            annotations[i] = r.Annotation;
            fors[i] = r.Fors;
            givens[i] = r.Givens;
            mustBes[i] = r.MustBe;
        }

        var forsJson = JsonSerializer.Serialize(fors, jsonOptions);
        var givensJson = JsonSerializer.Serialize(givens, jsonOptions);
        var mustBesJson = JsonSerializer.Serialize(mustBes, jsonOptions);

        var embeddings = embeddingService
            .GetBatchEmbeddings(annotations)
            .Select(v => new Vector(v))
            .ToArray();

        await using var cmd =
            new NpgsqlCommand(
                """
                insert into rules (id, community, annotation, fors, givens, must_be, annotation_embedding)
                select src.id, src.community, src.annotation, e1.fors, e2.givens, e3.must_be, src.annotation_embedding 
                from unnest($1, $2, $3, $7) with ordinality as src(id, community, annotation, annotation_embedding, idx)
                join lateral jsonb_array_elements($4) with ordinality as e1(fors, idx1) on src.idx = idx1
                join lateral jsonb_array_elements($5) with ordinality as e2(givens, idx2) on src.idx = idx2
                join lateral jsonb_array_elements($6) with ordinality as e3(must_be, idx3) on src.idx = idx3
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
        cmd.Parameters.Add(new NpgsqlParameter { Value = forsJson, NpgsqlDbType = NpgsqlDbType.Jsonb });
        cmd.Parameters.Add(new NpgsqlParameter { Value = givensJson, NpgsqlDbType = NpgsqlDbType.Jsonb });
        cmd.Parameters.Add(new NpgsqlParameter { Value = mustBesJson, NpgsqlDbType = NpgsqlDbType.Jsonb });
        cmd.Parameters.Add(new NpgsqlParameter { Value = embeddings, DataTypeName = "vector[]" });
        
        await cmd.ExecuteNonQueryAsync(cancellationToken);
        return true;
    }

    private static IEnumerable<T> ReadToEnumerable<T>(NpgsqlDataReader reader, Func<NpgsqlDataReader, T> func)
    {
        while (reader.Read()) yield return func(reader);
    }
}