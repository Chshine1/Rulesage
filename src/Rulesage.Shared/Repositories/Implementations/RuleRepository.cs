using System.Text.Json;
using Microsoft.FSharp.Collections;
using Npgsql;
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

    public async Task<RuleExpr?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var conn = dataSource.CreateConnection();
        await conn.OpenAsync(cancellationToken);

        await using var cmd =
            new NpgsqlCommand("SELECT annotation, fors, givens, must_be FROM rules WHERE id = $1", conn);
        cmd.Parameters.Add(id);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken)) return null;

        var annotation = reader.GetString(0);
        var forsJson = reader.GetString(1);
        var givensJson = reader.GetString(2);
        var mustBeJson = reader.GetString(3);

        var fors =
            JsonSerializer.Deserialize<FSharpMap<string, ForItem>>(forsJson, jsonOptions);

        var givens =
            JsonSerializer.Deserialize<FSharpMap<string, GivenItem>>(givensJson, jsonOptions);
        
        var mustBe = JsonSerializer.Deserialize<ValueExpr>(mustBeJson, jsonOptions);

        return new RuleExpr(annotation, id, fors, givens, mustBe);
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
                SELECT
                    id,
                    annotation,
                    fors,
                    givens,
                    mustBe,
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
            var id = r.GetString(0);
            var annotation = r.GetString(1);
            var forsJson = r.GetString(2);
            var givensJson = r.GetString(3);
            var mustBeJson = r.GetString(4);

            var fors =
                JsonSerializer.Deserialize<FSharpMap<string, ForItem>>(forsJson, jsonOptions);

            var givens =
                JsonSerializer.Deserialize<FSharpMap<string, GivenItem>>(givensJson, jsonOptions);
        
            var mustBe = JsonSerializer.Deserialize<ValueExpr>(mustBeJson, jsonOptions);

            return (
                new RuleExpr(annotation, id, fors, givens, mustBe),
                (float)r.GetDouble(5)
            );
        });
    }

    private static IEnumerable<T> ReadToEnumerable<T>(NpgsqlDataReader reader, Func<NpgsqlDataReader, T> func)
    {
        while (reader.Read()) yield return func(reader);
    }
}