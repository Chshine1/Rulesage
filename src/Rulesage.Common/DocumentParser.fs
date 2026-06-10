namespace Rulesage.Common

open FParsec
open Rulesage.Common.Grammar
open Rulesage.Common.Grammar.Ast
open Rulesage.Common.Grammar.Parsers.Domain.Action
open Rulesage.Common.Grammar.Parsers.Domain.Concept
open Rulesage.Common.Grammar.Parsers.Domain.Rule
open Rulesage.Common.Grammar.Parsers.Lexer

type RulesetSection =
    {
        Rules: RuleExpr list
        Records: ConceptExpr list
        Actions: ActionExpr list
    }

module DocumentParser =
    type private AstNode =
        | RuleDef of RuleExpr
        | ConceptDef of ConceptExpr
        | ActionDef of ActionExpr

    let private pAstNode: Parser<AstNode> =
        opt (skipChar '#' >>. pDomainTag .>> spaces1)
        >>= fun oDomain ->
            let domain = oDomain |> Option.defaultValue ""

            choice
                [
                    pConcept domain |>> ConceptDef
                    pAction domain |>> ActionDef
                    pRule domain |>> RuleDef
                ]

    let private pDocument: Parser<AstNode list> =
        spaces >>. many (pAstNode .>> spaces1) .>> eof

    let Parse (input: string) : RulesetSection =
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
                    | ConceptDef r -> Some r
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
