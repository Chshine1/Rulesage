namespace Rulesage.Common.Repositories.Implementations

open System.Text.Json
open Npgsql
open NpgsqlTypes
open Pgvector
open Rulesage.Common.Grammar.Ast
open Rulesage.Common.Repositories.Abstractions

type RuleRepository(dataSource: NpgsqlDataSource, jsonOptions: JsonSerializerOptions) =
    static let readAll (reader: NpgsqlDataReader) (f: NpgsqlDataReader -> 'T) : 'T list =
        [
            while reader.Read() do
                yield f reader
        ]

    let deserialize (json: string) : 'T =
        JsonSerializer.Deserialize<'T>(json, jsonOptions)
    
    let serialize target : string =
        JsonSerializer.Serialize(target, jsonOptions)

    interface IRuleRepository with
        member _.GetDocumentsAsync(cancellationToken) =
            task {
                use conn = dataSource.CreateConnection()
                do! conn.OpenAsync(cancellationToken)
                use cmd = new NpgsqlCommand("SELECT annotation FROM rules", conn)
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
                        select
                            id,
                            ignore,
                            community_id,
                            annotation,
                            fors,
                            givens,
                            must_be
                        from rules
                        where id=any($1)
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

                let idOrdinal = reader.GetOrdinal("id")
                let ignoreOrdinal = reader.GetOrdinal("ignore")
                let communityOrdinal = reader.GetOrdinal("community_id")
                let annotationOrdinal = reader.GetOrdinal("annotation")
                let forsOrdinal = reader.GetOrdinal("fors")
                let givensOrdinal = reader.GetOrdinal("givens")
                let mustBeOrdinal = reader.GetOrdinal("must_be")

                let results =
                    readAll
                        reader
                        (fun r ->
                            let id = r.GetString(idOrdinal)
                            let ignore = r.GetBoolean(ignoreOrdinal)
                            let community = r.GetString(communityOrdinal)
                            let annotation = r.GetString(annotationOrdinal)
                            let fors = r.GetString(forsOrdinal) |> deserialize
                            let givens = r.GetString(givensOrdinal) |> deserialize
                            let mustBe = r.GetString(mustBeOrdinal) |> deserialize

                            {
                                Id = id
                                Ignore = ignore
                                Community = community
                                Annotation = annotation
                                Fors = fors
                                Givens = givens
                                MustBe = mustBe
                            }
                        )

                return results :> RuleExpr seq
            }

        member _.FindOrderByCosineDistanceAsync(contextCommunity, queryVector, skip, take, cancellationToken) =
            task {
                use conn = dataSource.CreateConnection()
                do! conn.OpenAsync(cancellationToken)

                use cmd =
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
                        where (community_id = $2 or community_id = '') and ignore = false
                        order by distance
                        limit $3 offset $4;
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

                let idOrdinal = reader.GetOrdinal("id")
                let annotationOrdinal = reader.GetOrdinal("annotation")
                let forsOrdinal = reader.GetOrdinal("fors")
                let givensOrdinal = reader.GetOrdinal("givens")
                let mustBeOrdinal = reader.GetOrdinal("must_be")
                let distanceOrdinal = reader.GetOrdinal("distance")

                let results =
                    readAll
                        reader
                        (fun r ->
                            let id = r.GetString(idOrdinal)
                            let annotation = r.GetString(annotationOrdinal)
                            let fors = r.GetString(forsOrdinal) |> deserialize
                            let givens = r.GetString(givensOrdinal) |> deserialize
                            let mustBe = r.GetString(mustBeOrdinal) |> deserialize

                            let expr =
                                {
                                    Id = id
                                    Ignore = false
                                    Community = contextCommunity
                                    Annotation = annotation
                                    Fors = fors
                                    Givens = givens
                                    MustBe = mustBe
                                }

                            let similarity = 1.0f - float32 (r.GetDouble(distanceOrdinal))
                            expr, similarity
                        )

                return results :> (RuleExpr * float32) seq
            }

        member _.SaveAsync(rules, cancellationToken) =
            task {
                use conn = dataSource.CreateConnection()
                do! conn.OpenAsync(cancellationToken)

                let rulesArray = Seq.toArray rules
                let count = rulesArray.Length

                let ids = Array.zeroCreate<string> count
                let ignores = Array.zeroCreate<bool> count
                let communities = Array.zeroCreate<string> count
                let annotations = Array.zeroCreate<string> count
                let fors = Array.zeroCreate<Map<string, ParamExpr>> count
                let givens = Array.zeroCreate<Map<string, GivenExpr>> count
                let mustBes = Array.zeroCreate<ValueExpr> count

                for i in 0 .. count - 1 do
                    let r = rulesArray[i]
                    ids[i] <- r.Id
                    ignores[i] <- r.Ignore
                    communities[i] <- r.Community
                    annotations[i] <- r.Annotation
                    fors[i] <- r.Fors
                    givens[i] <- r.Givens
                    mustBes[i] <- r.MustBe

                let forsJson = serialize fors
                let givensJson = serialize givens
                let mustBesJson = serialize mustBes

                use cmd =
                    new NpgsqlCommand(
                        """
                        insert into rules (id, ignore, community_id, annotation, fors, givens, must_be)
                        select src.id, src.ignore, src.community, src.annotation, e1.fors, e2.givens, e3.must_be
                        from unnest($1, $2, $3, $4) with ordinality as src(id, ignore, community, annotation, idx)
                        join lateral jsonb_array_elements($5) with ordinality as e1(fors, idx1) on src.idx = idx1
                        join lateral jsonb_array_elements($6) with ordinality as e2(givens, idx2) on src.idx = idx2
                        join lateral jsonb_array_elements($7) with ordinality as e3(must_be, idx3) on src.idx = idx3
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

                cmd.Parameters.Add(NpgsqlParameter(Value = forsJson, NpgsqlDbType = NpgsqlDbType.Jsonb))
                |> ignore

                cmd.Parameters.Add(NpgsqlParameter(Value = givensJson, NpgsqlDbType = NpgsqlDbType.Jsonb))
                |> ignore

                cmd.Parameters.Add(NpgsqlParameter(Value = mustBesJson, NpgsqlDbType = NpgsqlDbType.Jsonb))
                |> ignore

                return! cmd.ExecuteNonQueryAsync(cancellationToken)
            }
