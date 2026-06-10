namespace Rulesage.Synthesis.Types

open Rulesage.Common.Grammar
open Rulesage.Common.Grammar.Ast

type InterpretedValue =
    | Literal of value: string
    | Concept of instance: ConceptInstance
    | Array of arr: InterpretedValue array

    member s.GetNodeField(fieldKey: string) : InterpretedValue =
        match s with
        | Concept i -> i.Arguments |> Map.find fieldKey
        | Array a -> a |> Array.map (fun e -> e.GetNodeField fieldKey) |> InterpretedValue.Array
        | _ -> failwith "Literal values cannot extract fields"


and ConceptInstance =
    {
        ConceptName: Identifier
        GenericArgs: TypeExpr list
        Arguments: Map<string, InterpretedValue>
    }
