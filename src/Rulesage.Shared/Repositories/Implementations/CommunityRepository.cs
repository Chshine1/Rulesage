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
        if (contextCommunity.Length == 0) return [];
        
        await using var conn = dataSource.CreateConnection();
        await conn.OpenAsync(cancellationToken);

        var sections = contextCommunity.Split('.');
        var parentHierarchy = new string[sections.Length + 1];
        var sectionSum = "";
        parentHierarchy[0] = "";
        for (var i = 0; i < sections.Length; i++)
        {
            sectionSum += sections[i];
            parentHierarchy[i + 1] = sectionSum;
        }

        var excludeHierarchy = parentHierarchy[1..];

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
        cmd.Parameters.Add(new NpgsqlParameter<string[]>
            { Value = parentHierarchy, NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text });
        cmd.Parameters.Add(new NpgsqlParameter<string[]>
            { Value = excludeHierarchy, NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text });
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
        var parentIds = new string[count];
        var annotations = new string[count];

        for (var i = 0; i < count; i++)
        {
            var c = communitiesArray[i];
            ids[i] = c.Sections.Aggregate((a, b) => $"{a}.{b}");
            var last = ids[i].LastIndexOf('.');
            parentIds[i] = last < 0 ? "" : ids[i][..last];
            annotations[i] = c.Annotation;
        }

        await using var cmd =
            new NpgsqlCommand(
                """
                insert into communities (id, parent_id, annotation)
                select src.id, src.parent_id, src.annotation
                from unnest($1, $2, $3) as src(id, parent_id, annotation)
                """,
                conn
            );

        // ReSharper disable BitwiseOperatorOnEnumWithoutFlags
        cmd.Parameters.Add(new NpgsqlParameter { Value = ids, NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text });
        cmd.Parameters.Add(new NpgsqlParameter { Value = parentIds, NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text });
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