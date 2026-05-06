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
        Outputs: Map<string, BlueprintValue>
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

    let key: Parser<string, IndentState> = regex "[a-zA-Z][a-zA-Z0-9]*"

    let stringLiteral: Parser<string, IndentState> =
        between (pstring "\"") (pstring "\"") (manyChars (noneOf "\""))

    let atomicType: Parser<ParamType, IndentState> =
        attempt (pstring "leaf" >>% ParamType.Leaf)
        <|> (pstring "node" >>. spaces1 >>. ir |>> fun ir -> ParamType.Node { id = -1; ir = ir })

    let paramType: Parser<ParamType, IndentState> =
        atomicType .>>. many (pstring "[]")
        |>> fun (baseTy, arrays) -> List.fold (fun acc _ -> ParamType.Array acc) baseTy arrays

    let paramDef: Parser<string * ParamType, IndentState> =
        key .>> spaces .>> pstring ":" .>> spaces .>>. paramType .>> skipNewline

    let valueExpr, valueExprRef =
        createParserForwardedToRef<BlueprintValue, IndentState> ()

    let argDef: Parser<string * BlueprintValue, IndentState> =
        key .>> spaces .>> pstring ":" .>> spaces .>>. valueExpr .>> skipNewline

    let argBlock: Parser<Map<string, BlueprintValue>, IndentState> =
        attempt (skipNewline >>. indentedBlock (many argDef) |>> Map.ofList)
        <|>% Map.empty
        
    let refSourceExpr: Parser<RefSource, IndentState> = 
        attempt (pstring "$args." >>. key |>> RefSource.FromParameter)
        <|> (pstring "$subtasks." >>. key .>> pstring "." .>>. key |>> RefSource.FromSubtask)      

    valueExprRef.Value <-
        attempt (refSourceExpr .>>. many (pstring "." >>. key) |>> BlueprintValue.Ref)
        <|> attempt (pstring "leaf" >>. spaces1 >>. stringLiteral |>> BlueprintValue.Leaf)
        <|> attempt (pstring "node" >>. spaces1 >>. ir .>>. argBlock |>> fun (nodeIr, args) -> BlueprintValue.NodeBlueprint({ id = -1; ir = nodeIr }, args ))
        <|> (between (pstring "[") (pstring "]") (sepBy (valueExpr .>> spaces) (pstring "," .>> spaces)) |>> (fun xs -> BlueprintValue.Array(Array.ofList xs)))

    let subtaskExpr, subtaskExprRef =
        createParserForwardedToRef<Subtask, IndentState> ()
        
    subtaskExprRef.Value <- attempt (pstring "op" >>. spaces1 >>. ir .>>. argBlock |>> fun (opIr, args) -> Subtask.InvokeOperation({ id = -1; ir = opIr }, args))
        <|> attempt (pstring "conv" >>. spaces1 >>. ir .>>. argBlock |>> fun (convIr, args) -> Subtask.InvokeConverter({ id = -1; ir = convIr }, args))
        <|> attempt (pstring "nl" >>. spaces1 >>. stringLiteral |>> Subtask.NlTask)
        <|> (pstring "map" >>. spaces1 >>. subtaskExpr |>> Subtask.Map)
    
    let subtaskDef: Parser<string * Subtask, IndentState> =
        key .>> spaces .>> pstring "=" .>> spaces .>>. subtaskExpr .>> skipNewline

    let outputDef: Parser<string * BlueprintValue, IndentState> =
        key .>> spaces .>> pstring ":" .>> spaces .>>. valueExpr .>> skipNewline

    let operationDef: Parser<OperationDefRecord, IndentState> =
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
        many operationDef .>> eof

    let parseDsl (input: string) : Result<OperationDefRecord list, string> =
        match runParserOnString operationFile initialState "" input with
        | Success(result, _, _) -> Result.Ok result
        | Failure(msg, _, _) -> Result.Error msg
