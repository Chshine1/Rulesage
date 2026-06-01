namespace Rulesage.Common.Repositories.Implementations

open System.Text.Json
open Npgsql
open NpgsqlTypes
open Pgvector
open Rulesage.Common.Grammar.Ast
open Rulesage.Common.Repositories.Abstractions

type ActionRepository(dataSource: NpgsqlDataSource, jsonOptions: JsonSerializerOptions) =
    static let readAll (reader: NpgsqlDataReader) (f: NpgsqlDataReader -> 'T) : 'T list =
        [
            while reader.Read() do
                yield f reader
        ]

    let deserialize (json: string) : 'T =
        JsonSerializer.Deserialize<'T>(json, jsonOptions)
    
    let serialize target : string =
        JsonSerializer.Serialize(target, jsonOptions)

    let mapToActionExpr
        (reader: NpgsqlDataReader)
        (idOrd: int)
        (ignoreOrd: int)
        (communityOrd: int)
        (annotationOrd: int)
        (genericParamsOrd: int)
        (forsOrd: int)
        (returnsOrd: int)
        (scriptOrd: int)
        (community: string option)
        =
        let communityValue =
            match community with
            | Some c -> c
            | None -> reader.GetString(communityOrd)

        {
            Id = reader.GetString(idOrd)
            Ignore =
                if community.IsSome then
                    false
                else
                    reader.GetBoolean(ignoreOrd)
            Community = communityValue
            Annotation = reader.GetString(annotationOrd)
            GenericParams = reader.GetString(genericParamsOrd) |> deserialize
            Fors = reader.GetString(forsOrd) |> deserialize
            Returns = reader.GetString(returnsOrd) |> deserialize
            Script = reader.GetString(scriptOrd)
        }

    interface IActionRepository with

        member _.GetDocumentsAsync(cancellationToken) =
            task {
                use conn = dataSource.CreateConnection()
                do! conn.OpenAsync(cancellationToken)
                use cmd = new NpgsqlCommand("SELECT annotation FROM actions", conn)
                use! reader = cmd.ExecuteReaderAsync(cancellationToken)
                let results = readAll reader _.GetString(0)
                return results :> string seq
            }

        member _.FindByIdsAsync(ids, cancellationToken) =
            task {
                use conn = dataSource.CreateConnection()
                do! conn.OpenAsync(cancellationToken)

                use cmd =
                    new NpgsqlCommand(
                        """
                        SELECT
                            id,
                            ignore,
                            community_id,
                            annotation,
                            generic_params,
                            fors,
                            returns,
                            script
                        FROM actions
                        WHERE id = ANY($1)
                        """,
                        conn
                    )

                cmd.Parameters.Add(
                    NpgsqlParameter<string[]>(
                        Value = Seq.toArray ids,
                        NpgsqlDbType = (NpgsqlDbType.Array ||| NpgsqlDbType.Text)
                    )
                )
                |> ignore

                use! reader = cmd.ExecuteReaderAsync(cancellationToken)

                let idOrd = reader.GetOrdinal("id")
                let ignoreOrd = reader.GetOrdinal("ignore")
                let communityOrd = reader.GetOrdinal("community_id")
                let annotationOrd = reader.GetOrdinal("annotation")
                let genericParamsOrd = reader.GetOrdinal("generic_params")
                let forsOrd = reader.GetOrdinal("fors")
                let returnsOrd = reader.GetOrdinal("returns")
                let scriptOrd = reader.GetOrdinal("script")

                let results =
                    readAll
                        reader
                        (fun r ->
                            mapToActionExpr
                                r
                                idOrd
                                ignoreOrd
                                communityOrd
                                annotationOrd
                                genericParamsOrd
                                forsOrd
                                returnsOrd
                                scriptOrd
                                None
                        )

                return results :> ActionExpr seq
            }

        member _.FindOrderByCosineDistanceAsync(contextCommunity, queryVector, skip, take, cancellationToken) =
            task {
                use conn = dataSource.CreateConnection()
                do! conn.OpenAsync(cancellationToken)

                use cmd =
                    new NpgsqlCommand(
                        """
                        SELECT
                            id,
                            annotation,
                            generic_params,
                            fors,
                            returns,
                            script,
                            (annotation_embedding <=> $1) AS distance
                        FROM actions
                        WHERE (community_id = $2 OR community_id = '') AND ignore = false
                        ORDER BY distance
                        LIMIT $3 OFFSET $4
                        """,
                        conn
                    )

                cmd.Parameters.Add(NpgsqlParameter(Value = Vector(queryVector), DataTypeName = "vector"))
                |> ignore

                cmd.Parameters.Add(NpgsqlParameter<string>(Value = contextCommunity, NpgsqlDbType = NpgsqlDbType.Text))
                |> ignore

                cmd.Parameters.Add(NpgsqlParameter<int>(Value = take, NpgsqlDbType = NpgsqlDbType.Integer))
                |> ignore

                cmd.Parameters.Add(NpgsqlParameter<int>(Value = skip, NpgsqlDbType = NpgsqlDbType.Integer))
                |> ignore

                use! reader = cmd.ExecuteReaderAsync(cancellationToken)

                let idOrd = reader.GetOrdinal("id")
                let annotationOrd = reader.GetOrdinal("annotation")
                let genericParamsOrd = reader.GetOrdinal("generic_params")
                let forsOrd = reader.GetOrdinal("fors")
                let returnsOrd = reader.GetOrdinal("returns")
                let scriptOrd = reader.GetOrdinal("script")
                let distanceOrd = reader.GetOrdinal("distance")

                let results =
                    readAll
                        reader
                        (fun r ->
                            let expr =
                                {
                                    Id = r.GetString(idOrd)
                                    Ignore = false
                                    Community = contextCommunity
                                    Annotation = r.GetString(annotationOrd)
                                    GenericParams = r.GetString(genericParamsOrd) |> deserialize
                                    Fors = r.GetString(forsOrd) |> deserialize
                                    Returns = r.GetString(returnsOrd) |> deserialize
                                    Script = r.GetString(scriptOrd)
                                }

                            let similarity = 1.0f - float32 (r.GetDouble(distanceOrd))
                            expr, similarity
                        )

                return results :> seq<ActionExpr * float32>
            }

        member _.SaveAsync(actions, cancellationToken) =
            task {
                let actionsArray = Seq.toArray actions

                let ids = actionsArray |> Array.map _.Id
                let ignores = actionsArray |> Array.map _.Ignore
                let communities = actionsArray |> Array.map _.Community
                let annotations = actionsArray |> Array.map _.Annotation
                let scripts = actionsArray |> Array.map _.Script

                let genericParamsJson =
                    actionsArray |> Array.map _.GenericParams |> serialize

                let forsJson = actionsArray |> Array.map _.Fors |> serialize
                let returnsJson = actionsArray |> Array.map _.Returns |> serialize

                use conn = dataSource.CreateConnection()
                do! conn.OpenAsync(cancellationToken)

                use cmd =
                    new NpgsqlCommand(
                        """
                        INSERT INTO actions (id, ignore, community_id, annotation, generic_params, fors, returns, script)
                        SELECT src.id, src.ignore, src.community, src.annotation, e1.generic_params, e2.fors, e3.returns, src.script
                        FROM unnest($1, $2, $3, $4, $8) WITH ORDINALITY AS src(id, ignore, community, annotation, script, idx)
                        JOIN LATERAL jsonb_array_elements($5) WITH ORDINALITY AS e1(generic_params, idx1) ON src.idx = idx1
                        JOIN LATERAL jsonb_array_elements($6) WITH ORDINALITY AS e2(fors, idx2) ON src.idx = idx2
                        JOIN LATERAL jsonb_array_elements($7) WITH ORDINALITY AS e3(returns, idx3) ON src.idx = idx3
                        """,
                        conn
                    )

                cmd.Parameters.Add(
                    NpgsqlParameter(Value = ids, NpgsqlDbType = (NpgsqlDbType.Array ||| NpgsqlDbType.Text))
                )
                |> ignore

                cmd.Parameters.Add(
                    NpgsqlParameter(Value = ignores, NpgsqlDbType = (NpgsqlDbType.Array ||| NpgsqlDbType.Boolean))
                )
                |> ignore

                cmd.Parameters.Add(
                    NpgsqlParameter(Value = communities, NpgsqlDbType = (NpgsqlDbType.Array ||| NpgsqlDbType.Text))
                )
                |> ignore

                cmd.Parameters.Add(
                    NpgsqlParameter(Value = annotations, NpgsqlDbType = (NpgsqlDbType.Array ||| NpgsqlDbType.Text))
                )
                |> ignore

                cmd.Parameters.Add(NpgsqlParameter(Value = genericParamsJson, NpgsqlDbType = NpgsqlDbType.Jsonb))
                |> ignore

                cmd.Parameters.Add(NpgsqlParameter(Value = forsJson, NpgsqlDbType = NpgsqlDbType.Jsonb))
                |> ignore

                cmd.Parameters.Add(NpgsqlParameter(Value = returnsJson, NpgsqlDbType = NpgsqlDbType.Jsonb))
                |> ignore

                cmd.Parameters.Add(
                    NpgsqlParameter(Value = scripts, NpgsqlDbType = (NpgsqlDbType.Array ||| NpgsqlDbType.Text))
                )
                |> ignore

                return! cmd.ExecuteNonQueryAsync(cancellationToken)
            }
