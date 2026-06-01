namespace Rulesage.Common.Repositories.Implementations

open Npgsql
open NpgsqlTypes
open Pgvector
open Rulesage.Common.Grammar.Ast
open Rulesage.Common.Repositories.Abstractions

type CommunityRepository(dataSource: NpgsqlDataSource) =
    static let readAll (reader: NpgsqlDataReader) (f: NpgsqlDataReader -> 'T) : 'T list =
        [
            while reader.Read() do
                yield f reader
        ]

    interface ICommunityRepository with
        member _.GetDocumentsAsync(cancellationToken) =
            task {
                use conn = dataSource.CreateConnection()
                do! conn.OpenAsync(cancellationToken)
                use cmd = new NpgsqlCommand("SELECT annotation FROM communities", conn)
                use! reader = cmd.ExecuteReaderAsync(cancellationToken)
                let results = readAll reader _.GetString(0)
                return results :> string seq
            }

        member _.FindOrderByCosineDistanceAsync(contextCommunity, queryVector, skip, take, cancellationToken) =
            task {
                use conn = dataSource.CreateConnection()
                do! conn.OpenAsync(cancellationToken)

                let sections = contextCommunity.Split('.')
                let parentHierarchy = Array.zeroCreate<string> (sections.Length + 1)
                let mutable sectionSum = ""
                parentHierarchy[0] <- ""

                for i in 0 .. sections.Length - 1 do
                    sectionSum <- sectionSum + sections[i]
                    parentHierarchy[i + 1] <- sectionSum

                let excludeHierarchy = parentHierarchy[1..]

                use cmd =
                    new NpgsqlCommand(
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
                        conn
                    )

                cmd.Parameters.Add(NpgsqlParameter(Value = Vector(queryVector), DataTypeName = "vector"))
                |> ignore

                cmd.Parameters.Add(
                    NpgsqlParameter<string[]>(
                        Value = parentHierarchy,
                        NpgsqlDbType = (NpgsqlDbType.Array ||| NpgsqlDbType.Text)
                    )
                )
                |> ignore

                cmd.Parameters.Add(
                    NpgsqlParameter<string[]>(
                        Value = excludeHierarchy,
                        NpgsqlDbType = (NpgsqlDbType.Array ||| NpgsqlDbType.Text)
                    )
                )
                |> ignore

                cmd.Parameters.Add(NpgsqlParameter<int>(Value = take, NpgsqlDbType = NpgsqlDbType.Integer))
                |> ignore

                cmd.Parameters.Add(NpgsqlParameter<int>(Value = skip, NpgsqlDbType = NpgsqlDbType.Integer))
                |> ignore

                use! reader = cmd.ExecuteReaderAsync(cancellationToken)

                let idOrdinal = reader.GetOrdinal("id")
                let annotationOrdinal = reader.GetOrdinal("annotation")
                let distanceOrdinal = reader.GetOrdinal("distance")

                let results =
                    readAll
                        reader
                        (fun r ->
                            let id = r.GetString(idOrdinal)
                            let annotation = r.GetString(annotationOrdinal)

                            let expr =
                                {
                                    Sections = id.Split('.')
                                    Annotation = annotation
                                }

                            let similarity = 1.0f - float32 (r.GetDouble(distanceOrdinal))
                            expr, similarity
                        )

                return results :> (CommunityExpr * float32) seq
            }

        member _.SaveAsync(communities, cancellationToken) =
            task {
                use conn = dataSource.CreateConnection()
                do! conn.OpenAsync(cancellationToken)

                let communitiesArray = Seq.toArray communities
                let count = communitiesArray.Length

                let ids = Array.zeroCreate<string> count
                let parentIds = Array.zeroCreate<string> count
                let annotations = Array.zeroCreate<string> count

                for i in 0 .. count - 1 do
                    let c = communitiesArray[i]
                    let id = c.Sections |> String.concat "."
                    ids[i] <- id
                    let lastDot = id.LastIndexOf('.')
                    parentIds[i] <- if lastDot < 0 then "" else id.Substring(0, lastDot)
                    annotations[i] <- c.Annotation

                use cmd =
                    new NpgsqlCommand(
                        """
                        insert into communities (id, parent_id, annotation)
                        select src.id, src.parent_id, src.annotation
                        from unnest($1, $2, $3) as src(id, parent_id, annotation)
                        """,
                        conn
                    )

                cmd.Parameters.Add(
                    NpgsqlParameter(Value = ids, NpgsqlDbType = (NpgsqlDbType.Array ||| NpgsqlDbType.Text))
                )
                |> ignore

                cmd.Parameters.Add(
                    NpgsqlParameter(Value = parentIds, NpgsqlDbType = (NpgsqlDbType.Array ||| NpgsqlDbType.Text))
                )
                |> ignore

                cmd.Parameters.Add(
                    NpgsqlParameter(Value = annotations, NpgsqlDbType = (NpgsqlDbType.Array ||| NpgsqlDbType.Text))
                )
                |> ignore

                return! cmd.ExecuteNonQueryAsync(cancellationToken)
            }
