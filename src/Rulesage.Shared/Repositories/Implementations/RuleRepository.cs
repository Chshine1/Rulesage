using System.Text.Json;
using Microsoft.FSharp.Collections;
using Npgsql;
using NpgsqlTypes;
using Pgvector;
using Rulesage.Common.Grammar.Ast;
using Rulesage.Shared.Repositories.Abstractions;

namespace Rulesage.Shared.Repositories.Implementations;

public class RuleRepository(NpgsqlDataSource dataSource, JsonSerializerOptions jsonOptions) : IRuleRepository
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

    public Task<bool> SaveAsync(IEnumerable<RuleExpr> rules, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    private static IEnumerable<T> ReadToEnumerable<T>(NpgsqlDataReader reader, Func<NpgsqlDataReader, T> func)
    {
        while (reader.Read()) yield return func(reader);
    }
}