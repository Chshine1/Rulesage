using System.Text.Json;
using Microsoft.FSharp.Collections;
using Npgsql;
using NpgsqlTypes;
using Pgvector;
using Rulesage.Common.Grammar.Ast;
using Rulesage.Shared.Repositories.Abstractions;

namespace Rulesage.Shared.Repositories.Implementations;

public class ActionRepository(NpgsqlDataSource dataSource, JsonSerializerOptions jsonOptions) : IActionRepository
{
    public async Task<IEnumerable<string>> GetDocumentsAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = dataSource.CreateConnection();
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand("SELECT annotation FROM actions", conn);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        return ReadToList(reader, r => r.GetString(0));
    }

    public async Task<IEnumerable<ActionExpr>> FindByIdsAsync(IEnumerable<string> ids,
        CancellationToken cancellationToken = default)
    {
        await using var conn = dataSource.CreateConnection();
        await conn.OpenAsync(cancellationToken);

        await using var cmd =
            new NpgsqlCommand(
                """
                select
                    id,
                    community_id,
                    annotation,
                    generic_params,
                    fors,
                    returns,
                    script
                from actions
                where id=any($1) 
                """,
                conn
            );

        // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
        cmd.Parameters.Add(new NpgsqlParameter<string[]>
            { Value = ids.ToArray(), NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text });

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var idOrdinal = reader.GetOrdinal("id");
        var communityOrdinal = reader.GetOrdinal("community_id");
        var annotationOrdinal = reader.GetOrdinal("annotation");
        var genericParamsOrdinal = reader.GetOrdinal("generic_params");
        var forsOrdinal = reader.GetOrdinal("fors");
        var returnsOrdinal = reader.GetOrdinal("returns");
        var scriptOrdinal = reader.GetOrdinal("script");

        return ReadToList(reader, r =>
        {
            var id = r.GetString(idOrdinal);
            var community = r.GetString(communityOrdinal);
            var annotation = r.GetString(annotationOrdinal);
            var genericParamsJson = r.GetString(genericParamsOrdinal);
            var forsJson = r.GetString(forsOrdinal);
            var returnsJson = r.GetString(returnsOrdinal);
            var script = r.GetString(scriptOrdinal);

            var genericParams =
                JsonSerializer.Deserialize<FSharpList<string>>(genericParamsJson, jsonOptions);
            var fors =
                JsonSerializer.Deserialize<FSharpMap<string, ParamExpr>>(forsJson, jsonOptions);
            var returns =
                JsonSerializer.Deserialize<TypeExpr>(returnsJson, jsonOptions);

            return new ActionExpr(id, community, annotation, genericParams, fors, returns, script);
        });
    }

    public async Task<IEnumerable<(ActionExpr, float)>> FindOrderByCosineDistanceAsync(string contextCommunity,
        float[] queryVector,
        int skip, int take,
        CancellationToken cancellationToken = default)
    {
        await using var conn = dataSource.CreateConnection();
        await conn.OpenAsync(cancellationToken);

        var sections = contextCommunity.Split('.');
        var hierarchy = new string[sections.Length + 1];
        var sectionSum = "";
        hierarchy[0] = "";
        for (var i = 0; i < sections.Length; i++)
        {
            sectionSum += sections[i];
            hierarchy[i + 1] = sectionSum;
        }

        await using var cmd =
            new NpgsqlCommand(
                """
                select
                    id,
                    annotation,
                    generic_params,
                    fors,
                    returns,
                    script,
                    (annotation_embedding <=> $1) as distance
                from actions
                where community_id = any($2)
                order by distance
                limit $3 offset $4;
                """,
                conn
            );

        cmd.Parameters.Add(new NpgsqlParameter { Value = new Vector(queryVector), DataTypeName = "vector" });
        // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
        cmd.Parameters.Add(new NpgsqlParameter<string[]>
            { Value = hierarchy, NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text });
        cmd.Parameters.Add(new NpgsqlParameter<int> { Value = take, NpgsqlDbType = NpgsqlDbType.Integer });
        cmd.Parameters.Add(new NpgsqlParameter<int> { Value = skip, NpgsqlDbType = NpgsqlDbType.Integer });

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var idOrdinal = reader.GetOrdinal("id");
        var annotationOrdinal = reader.GetOrdinal("annotation");
        var genericParamsOrdinal = reader.GetOrdinal("generic_params");
        var forsOrdinal = reader.GetOrdinal("fors");
        var returnsOrdinal = reader.GetOrdinal("returns");
        var scriptOrdinal = reader.GetOrdinal("script");
        var distanceOrdinal = reader.GetOrdinal("distance");

        return ReadToList(reader, r =>
        {
            var id = r.GetString(idOrdinal);
            var annotation = r.GetString(annotationOrdinal);
            var genericParamsJson = r.GetString(genericParamsOrdinal);
            var forsJson = r.GetString(forsOrdinal);
            var returnsJson = r.GetString(returnsOrdinal);
            var script = r.GetString(scriptOrdinal);

            var genericParams =
                JsonSerializer.Deserialize<FSharpList<string>>(genericParamsJson, jsonOptions);
            var fors =
                JsonSerializer.Deserialize<FSharpMap<string, ParamExpr>>(forsJson, jsonOptions);
            var returns =
                JsonSerializer.Deserialize<TypeExpr>(returnsJson, jsonOptions);

            return (
                new ActionExpr(id, contextCommunity, annotation, genericParams, fors, returns, script),
                1f - (float)r.GetDouble(distanceOrdinal)
            );
        });
    }

    public async Task<bool> SaveAsync(IEnumerable<ActionExpr> actions, CancellationToken cancellationToken = default)
    {
        await using var conn = dataSource.CreateConnection();
        await conn.OpenAsync(cancellationToken);

        var actionsArray = actions.ToArray();
        var count = actionsArray.Length;

        var ids = new string[count];
        var communities = new string[count];
        var annotations = new string[count];
        var genericParams = new FSharpList<string>[count];
        var fors = new FSharpMap<string, ParamExpr>[count];
        var returns = new TypeExpr[count];
        var scripts = new string[count];

        for (var i = 0; i < count; i++)
        {
            var r = actionsArray[i];
            ids[i] = r.Id;
            communities[i] = r.Community;
            annotations[i] = r.Annotation;
            genericParams[i] = r.GenericParams;
            fors[i] = r.Fors;
            returns[i] = r.Returns;
            scripts[i] = r.Script;
        }

        var genericParamsJson = JsonSerializer.Serialize(genericParams, jsonOptions);
        var forsJson = JsonSerializer.Serialize(fors, jsonOptions);
        var returnsJson = JsonSerializer.Serialize(returns, jsonOptions);

        await using var cmd =
            new NpgsqlCommand(
                """
                insert into actions (id, community_id, annotation, generic_params, fors, returns, script)
                select src.id, src.community, src.annotation, e1.generic_params, e2.fors, e3.returns, src.script
                from unnest($1, $2, $3, $7) with ordinality as src(id, community, annotation, script, idx)
                join lateral jsonb_array_elements($4) with ordinality as e1(generic_params, idx1) on src.idx = idx1
                join lateral jsonb_array_elements($5) with ordinality as e2(fors, idx2) on src.idx = idx2
                join lateral jsonb_array_elements($6) with ordinality as e3(returns, idx3) on src.idx = idx3
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
        cmd.Parameters.Add(new NpgsqlParameter { Value = returnsJson, NpgsqlDbType = NpgsqlDbType.Jsonb });
        // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
        cmd.Parameters.Add(new NpgsqlParameter
            { Value = scripts, NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text });

        await cmd.ExecuteNonQueryAsync(cancellationToken);
        return true;
    }

    private static List<T> ReadToList<T>(NpgsqlDataReader reader, Func<NpgsqlDataReader, T> func)
    {
        var result = new List<T>();
        while (reader.Read()) result.Add(func(reader));
        return result;
    }
}