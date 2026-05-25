namespace Rulesage.Common

open FParsec
open Rulesage.Common.Grammar
open Rulesage.Common.Grammar.Ast
open Rulesage.Common.Grammar.Parsers.Domain.Rule

type Document =
    {
        Rules: RuleExpr list
        Records: RecordExpr list
        Actions: ActionExpr list
    }

module DocumentParser =
    type private AstNode =
        | RuleDef of RuleExpr
        | RecordDef of RecordExpr
        | ActionDef of ActionExpr

    let private pAstNode =
        choice [ pRule |>> RuleDef; pRecord |>> RecordDef; pAction |>> ActionDef ]

    let private pDocument: Parser<AstNode list> =
        spaces >>. sepEndBy pAstNode spaces1 .>> eof

    let Parse (input: string) : Document =
        match run pDocument input with
        | Success(nodes, _, _) ->
            let rules =
                nodes
                |> List.choose (
                    function
                    | RuleDef r -> Some r
                    | _ -> None
                )

            let records =
                nodes
                |> List.choose (
                    function
                    | RecordDef r -> Some r
                    | _ -> None
                )

            let actions =
                nodes
                |> List.choose (
                    function
                    | ActionDef a -> Some a
                    | _ -> None
                )

            {
                Rules = rules
                Records = records
                Actions = actions
            }
        | Failure(msg, _, _) -> failwith msg
