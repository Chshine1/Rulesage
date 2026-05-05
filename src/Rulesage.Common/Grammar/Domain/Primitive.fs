namespace Rulesage.Common.Grammar.Domain

open FParsec
open Rulesage.Common.Types.Domain

type OperationDefRecord =
    {
        Id: string
        Description: string
        Level: float32
        Parameters: Map<string, ParamType>
        Subtasks: Map<string, Subtask>
        Outputs: Map<string, NodeBlueprint>
    }


type IndentState = { Stack: int64 list }

module Primitive =
    let initialState = { Stack = [ 1L ] }

    let isIndented (pos: Position) (state: IndentState) =
        match state.Stack with
        | [] -> true
        | top :: _ -> pos.Column > top

    let pushIndent (col: int64) : Parser<unit, IndentState> =
        updateUserState (fun us -> { us with Stack = col :: us.Stack })

    let popIndent: Parser<unit, IndentState> =
        updateUserState (fun us -> { us with Stack = us.Stack.Tail })

    let indentedBlock (p: Parser<'a, IndentState>) : Parser<'a, IndentState> =
        skipNewline >>. getPosition
        >>= fun pos ->
            userStateSatisfies (fun us -> pos.Column > (if us.Stack.IsEmpty then 1L else us.Stack.Head))
            >>. pushIndent pos.Column
            >>. p
            .>> popIndent

    let ir: Parser<string, IndentState> = regex "[a-zA-Z-][a-zA-Z0-9-]*"

    let key: Parser<string, IndentState> = regex "[a-zA-Z_][a-zA-Z0-9_]*"

    let stringLiteral: Parser<string, IndentState> =
        between (pstring "\"") (pstring "\"") (manyChars (noneOf "\""))

    let atomicType, atomicTypeRef =
        createParserForwardedToRef<ParamType, IndentState> ()

    let typeExpr: Parser<ParamType, IndentState> =
        atomicType .>>. many (pstring "[]")
        |>> fun (baseTy, arrays) -> List.fold (fun acc _ -> ParamType.Array acc) baseTy arrays

    atomicTypeRef.Value <-
        attempt (pstring "leaf" >>% ParamType.Leaf)
        <|> (pstring "node" >>. spaces1 >>. ir |>> fun ir -> Node { id = -1; ir = ir })

    let paramDef: Parser<string * ParamType, IndentState> =
        key .>> spaces .>> pstring ":" .>> spaces .>>. typeExpr .>> skipNewline

    let refExpr: Parser<BlueprintValue, IndentState> =
        pstring "$" >>. key .>>. many (pstring "." >>. key)
        |>> fun (first, rest) ->
            match rest with
            | [] -> FromParameter first
            | _ -> FromSubtask(first, String.concat "." rest)

    let valueExpr, valueExprRef =
        createParserForwardedToRef<BlueprintValue, IndentState> ()

    let argDef: Parser<string * BlueprintValue, IndentState> =
        key .>> spaces .>> pstring ":" .>> spaces .>>. valueExpr .>> skipNewline

    let argBlock: Parser<Map<string, BlueprintValue>, IndentState> =
        skipNewline >>. indentedBlock (many argDef) |>> Map.ofList

    let nodeBlueprintValue: Parser<NodeBlueprint, IndentState> =
        pstring "node" >>. spaces1 >>. ir .>>. argBlock
        |>> fun (nodeIr, args) ->
            {
                node = { id = -1; ir = nodeIr }
                args = args
            }

    let leafValue: Parser<BlueprintValue, IndentState> =
        pstring "leaf" >>. spaces1 >>. stringLiteral |>> Leaf

    let arrayValue: Parser<BlueprintValue, IndentState> =
        between (pstring "[") (pstring "]") (sepBy (valueExpr .>> spaces) (pstring "," .>> spaces))
        |>> (fun xs -> Array(Array.ofList xs))

    valueExprRef.Value <-
        attempt refExpr
        <|> attempt leafValue
        <|> attempt (nodeBlueprintValue |>> NodeBlueprint)
        <|> arrayValue

    let subtaskDef, subtaskDefRef =
        createParserForwardedToRef<string * Subtask, IndentState> ()

    let opSubtask: Parser<Subtask, IndentState> =
        pstring "op" >>. spaces1 >>. ir .>>. argBlock
        |>> fun (opIr, args) -> InvokeOperation({ id = -1; ir = opIr }, args)

    let convSubtask: Parser<Subtask, IndentState> =
        pstring "conv" >>. spaces1 >>. ir .>>. argBlock
        |>> fun (convIr, args) -> InvokeConverter({ id = -1; ir = convIr }, args)

    let nlSubtask: Parser<Subtask, IndentState> =
        pstring "nl" >>. spaces1 >>. stringLiteral |>> NlTask

    let subtaskExpr: Parser<Subtask, IndentState> =
        opSubtask <|> convSubtask <|> nlSubtask

    let mapSubtask: Parser<Subtask, IndentState> =
        pstring "map" >>. spaces1 >>. subtaskExpr |>> Subtask.Map

    subtaskDefRef.Value <-
        key .>> spaces .>> pstring "=" .>> spaces .>>. (subtaskExpr <|> mapSubtask) .>> skipNewline

    let outputDef: Parser<string * NodeBlueprint, IndentState> =
        key .>> spaces .>> pstring ":" .>> spaces .>>. valueExpr .>> skipNewline
        >>= fun (k, v) ->
            match v with
            | NodeBlueprint n -> preturn (k, n)
            | _ -> fail $"outputs only allows nodes, get %A{v} instead"

    let operationDef: Parser<OperationDefRecord, IndentState> =
        pstring "operation" >>. spaces1 >>. ir
        .>> spaces
        .>> pstring ":"
        .>> skipNewline
        >>. indentedBlock (
            pstring "desc" >>. spaces1 >>. stringLiteral .>> skipNewline
            .>>. (pstring "params" >>. spaces .>> pstring ":" .>> skipNewline
                  >>. indentedBlock (many paramDef))
            .>>. (pstring "steps" >>. spaces .>> pstring ":" .>> skipNewline
                  >>. indentedBlock (many subtaskDef))
            .>>. (pstring "outputs" >>. spaces .>> pstring ":" .>> skipNewline
                  >>. indentedBlock (many outputDef))
        )
        |>> fun (((desc, params_), subtasks_), outputs_) ->
            {
                Id = ""
                Description = desc
                Level = 0.0f
                Parameters = Map.ofList params_
                Subtasks = Map.ofList subtasks_
                Outputs = Map.ofList outputs_
            }

    let operationDefFinal: Parser<OperationDefRecord, IndentState> =
        let inner (name: string) =
            pstring "desc" >>. spaces1 >>. stringLiteral .>> skipNewline
            .>>. (pstring "params" >>. spaces .>> pstring ":" .>> skipNewline
                  >>. indentedBlock (many paramDef))
            .>>. (pstring "steps" >>. spaces .>> pstring ":" .>> skipNewline
                  >>. indentedBlock (many subtaskDef))
            .>>. (pstring "outputs" >>. spaces .>> pstring ":" .>> skipNewline
                  >>. indentedBlock (many outputDef))
            |>> fun (((desc, params_), subtasks_), outputs_) ->
                {
                    Id = name
                    Description = desc
                    Level = 0.0f
                    Parameters = Map.ofList params_
                    Subtasks = Map.ofList subtasks_
                    Outputs = Map.ofList outputs_
                }

        pstring "operation" >>. spaces1 >>. ir
        .>> spaces
        .>> pstring ":"
        .>> skipNewline
        >>= fun name -> indentedBlock (inner name)

    let operationFile: Parser<OperationDefRecord list, IndentState> =
        many operationDefFinal .>> eof

    let parseDsl (input: string) : Result<OperationDefRecord list, string> =
        match runParserOnString operationFile initialState "" input with
        | Success(result, _, _) -> Result.Ok result
        | Failure(msg, _, _) -> Result.Error msg
