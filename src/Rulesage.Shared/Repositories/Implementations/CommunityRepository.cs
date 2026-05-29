using Npgsql;
using NpgsqlTypes;
using Rulesage.Common.Grammar.Ast;
using Rulesage.Shared.Repositories.Abstractions;

namespace Rulesage.Shared.Repositories.Implementations;

public class CommunityRepository(NpgsqlDataSource dataSource) : ICommunityRepository
{
    public async Task<IEnumerable<string>> GetDocumentsAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = dataSource.CreateConnection();
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand("SELECT annotation FROM communities", conn);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        return ReadToList(reader, r => r.GetString(0));
    }

    public async Task<IEnumerable<(CommunityExpr, float)>> FindOrderByCosineDistanceAsync(
        string contextCommunity,
        float[] queryVector,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        await using var conn = dataSource.CreateConnection();
        await conn.OpenAsync(cancellationToken);

        var sections = contextCommunity.Split('.');
        var hierarchy = new string?[sections.Length];
        var sectionSum = "";
        hierarchy[0] = null;
        for (var i = 0; i < sections.Length; i++)
        {
            sectionSum += sections[i];
            hierarchy[i + 1] = sectionSum;
        }

        var hierarchyIds = hierarchy[1..^1];

        await using var cmd = new NpgsqlCommand(
            """
            select
                id,
                annotation,
                (annotation_embedding <=> $1) as distance
            from communities
            where parent_id = any($2)
              and not (id = any($3))
            order by distance
            limit $4 offset $5
            """,
            conn);

        // ReSharper disable BitwiseOperatorOnEnumWithoutFlags
        cmd.Parameters.Add(new NpgsqlParameter<float[]>
            { Value = queryVector, NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Real });
        cmd.Parameters.Add(new NpgsqlParameter<string?[]>
            { Value = hierarchy, NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text });
        cmd.Parameters.Add(new NpgsqlParameter<string[]>
            { Value = hierarchyIds, NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text });
        // ReSharper restore BitwiseOperatorOnEnumWithoutFlags

        cmd.Parameters.Add(new NpgsqlParameter<int> { Value = take });
        cmd.Parameters.Add(new NpgsqlParameter<int> { Value = skip });

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var idOrdinal = reader.GetOrdinal("id");
        var annotationOrdinal = reader.GetOrdinal("annotation");
        var distanceOrdinal = reader.GetOrdinal("distance");

        return ReadToList(reader, r =>
        {
            var id = r.GetString(idOrdinal);
            var annotation = r.GetString(annotationOrdinal);
            return (new CommunityExpr(id.Split('.'), annotation), 1f - (float)r.GetDouble(distanceOrdinal));
        });
    }

    public async Task<bool> SaveAsync(IEnumerable<CommunityExpr> communities,
        CancellationToken cancellationToken = default)
    {
        await using var conn = dataSource.CreateConnection();
        await conn.OpenAsync(cancellationToken);

        var communitiesArray = communities.ToArray();
        var count = communitiesArray.Length;

        var ids = new string[count];
        var annotations = new string[count];

        for (var i = 0; i < count; i++)
        {
            var r = communitiesArray[i];
            ids[i] = string.Concat(r.Sections);
            annotations[i] = r.Annotation;
        }

        await using var cmd =
            new NpgsqlCommand(
                """
                insert into communities (id, annotation)
                select src.id, src.annotation
                from unnest($1, $2) as src(id, annotation)
                """,
                conn
            );

        // ReSharper disable BitwiseOperatorOnEnumWithoutFlags
        cmd.Parameters.Add(new NpgsqlParameter { Value = ids, NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text });
        cmd.Parameters.Add(new NpgsqlParameter
            { Value = annotations, NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text });
        // ReSharper restore BitwiseOperatorOnEnumWithoutFlags

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