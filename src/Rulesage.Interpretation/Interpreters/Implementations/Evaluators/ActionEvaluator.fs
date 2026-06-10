namespace Rulesage.Interpretation.Interpreters.Implementations.Evaluators

open FParsec
open MoonSharp.Interpreter
open Rulesage.Common.Grammar.Ast
open Rulesage.Common.Grammar.Parsers.Types
open Rulesage.Interpretation.Interpreters.Abstractions
open Rulesage.Synthesis.Types

type ActionEvaluator() =
    let rec interpretedValueToDynValue (script: Script) (iv: InterpretedValue) : DynValue =
        match iv with
        | Literal value -> DynValue.NewString(value)
        | Concept instance ->
            let table = DynValue.NewTable(script)
            table.Table["__concept"] <- DynValue.NewBoolean(true)
            table.Table["ConceptName"] <- DynValue.NewString(instance.ConceptName)

            table.Table["GenericArgs"] <-
                DynValue.NewTuple(
                    instance.GenericArgs
                    |> List.map (fun t -> DynValue.NewString $"%A{t}")
                    |> Array.ofList
                )

            let argsTable = DynValue.NewTable(script)

            for kv in instance.Arguments do
                argsTable.Table[kv.Key] <- interpretedValueToDynValue script kv.Value

            table.Table["Arguments"] <- argsTable
            table
        | Array arr ->
            let table = DynValue.NewTable(script)

            arr
            |> Array.iteri (fun i v -> table.Table[i + 1] <- interpretedValueToDynValue script v)

            table

    let rec dynValueToInterpretedValue (dv: DynValue) : InterpretedValue =
        match dv.Type with
        | DataType.String -> Literal(dv.String)
        | DataType.Number -> Literal(string dv.Number)
        | DataType.Boolean -> Literal(if dv.Boolean then "true" else "false")
        | DataType.Table ->
            let table = dv.Table

            let isConcept =
                table.RawGet("__concept")
                |> Option.ofObj
                |> Option.map (fun v -> v.Type = DataType.Boolean && v.Boolean)
                |> Option.defaultValue false

            if isConcept then
                let conceptName = table.Get("ConceptName").String

                let genericArgs =
                    table.Get("GenericArgs").Tuple
                    |> Seq.map (fun v ->
                        match run pTypeExpr v.String with
                        | Success(t, _, _) -> t
                        | Failure(f, _, _) -> failwith f
                    )
                    |> Seq.toList

                let args =
                    table.Get("Arguments").Table.Pairs
                    |> Seq.map (fun p -> p.Key.String, dynValueToInterpretedValue p.Value)
                    |> Map.ofSeq

                Concept
                    {
                        ConceptName = conceptName
                        GenericArgs = genericArgs
                        Arguments = args
                    }
            else
                let len = table.Length

                if len > 0 && table.Keys |> Seq.forall (fun k -> k.Type = DataType.Number) then
                    let items =
                        [|
                            for i in 1..len do
                                yield dynValueToInterpretedValue (table.Get(i))
                        |]

                    Array items
                else
                    failwith "Returning arbitrary tables is not supported yet. Please return a Concept or Array."
        | _ -> failwith $"Unsupported Lua return type: {dv.Type}"

    interface IDynamicUnitEvaluator<ActionExpr> with
        member this.EvaluateAsync cancellationToken expr genericArgs args = task { }
