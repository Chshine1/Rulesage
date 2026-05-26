namespace Rulesage.Graph.Services.Implementations

open System
open Microsoft.Extensions.Options
open Rulesage.Graph
open Rulesage.Graph.Services.Abstractions

type DescriptionCleaner(config: IOptions<GraphConfig>) =
    let _config = config.Value

    interface IDescriptionCleaner with
        member _.Clean size descriptions =
            let tokenizedDocs =
                descriptions
                |> Seq.map
                    _.Split([| ' '; '\n'; '\t'; '.'; ','; '"'; '('; ')' |], StringSplitOptions.RemoveEmptyEntries)

            let df =
                tokenizedDocs
                |> Seq.collect (fun words -> words |> Set.ofArray)
                |> Seq.groupBy id
                |> Seq.map (fun (word, occurrences) -> word, float (Seq.length occurrences))
                |> Map.ofSeq

            printfn $"[DEBUG] Number of total documents: %d{size}"
            printfn $"[DEBUG] Number of distinct words: %d{df.Count}"

            let idf word =
                match Map.tryFind word df with
                | Some dfValue -> Math.Log((float size + 1.0) / (dfValue + 1.0))
                | _ -> 0.0

            tokenizedDocs
            |> Seq.map (fun words ->
                let tfMap =
                    words
                    |> Array.groupBy id
                    |> Array.map (fun (w, arr) -> w, 1.0 + log (float arr.Length))
                    |> Map.ofArray

                printfn $"\n[DEBUG] Original document words count=%d{words.Length}"
                printfn "  Original document: %s" (String.concat " " words)

                let k = max 5 (int (_config.TfIdfThreshold * float words.Length))
                let dWords = words |> Array.distinct

                let topWords =
                    dWords
                    |> Array.map (fun w ->
                        let tf = tfMap |> Map.tryFind w |> Option.defaultValue 0.0
                        let idfVal = idf w
                        let tfidf = tf * idfVal

                        printfn $"    [Word: %-20s{w}] TF=%.2f{tf}, IDF=%.4f{idfVal}, TF-IDF=%.4f{tfidf}"
                        w, tfidf
                    )
                    |> Array.sortByDescending snd
                    |> Array.take (min k dWords.Length)
                    |> Array.map fst

                let cleaned =
                    words
                    |> Array.filter (fun w -> Set.contains w (topWords |> Set.ofArray))
                    |> String.concat " "

                printfn $"  Cleaned: %s{cleaned}"
                cleaned
            )
