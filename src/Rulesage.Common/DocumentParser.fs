namespace Rulesage.Common

open System
open FParsec
open Rulesage.Common.Grammar
open Rulesage.Common.Grammar.Ast
open Rulesage.Common.Grammar.Parsers.Domain.Rule
open Rulesage.Common.Grammar.Parsers.Lexer
open Rulesage.Common.Grammar.Parsers.Strings

type Document =
    {
        Rules: RuleExpr list
        Records: RecordExpr list
        Actions: ActionExpr list
        Communities: CommunityExpr list
    }

module DocumentParser =
    type private AstNode =
        | RuleDef of RuleExpr
        | RecordDef of RecordExpr
        | ActionDef of ActionExpr
        | CommunityDef of CommunityExpr

    let private pAstNode: Parser<AstNode> =
        opt (skipChar '#' >>. pCommunityTag .>> spaces1) .>>. pAnnotation .>> spaces
        >>= fun (oCommunity, annotation) ->
            let community = oCommunity |> Option.defaultValue ""
            let parts = community.Split('.')
            let lastIsIgnore = parts.Length > 0 && parts[parts.Length - 1] = "ignore"

            let newCommunity =
                if lastIsIgnore then
                    String.Join(".", parts[.. parts.Length - 2])
                else
                    community

            choice
                [
                    pRule lastIsIgnore newCommunity annotation |>> RuleDef
                    pRecord lastIsIgnore newCommunity annotation |>> RecordDef
                    pAction lastIsIgnore newCommunity annotation |>> ActionDef
                    pCommunity community annotation |>> CommunityDef
                ]

    let private pDocument: Parser<AstNode list> =
        spaces >>. many (pAstNode .>> spaces1) .>> eof

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

            let communities =
                nodes
                |> List.choose (
                    function
                    | CommunityDef a -> Some a
                    | _ -> None
                )

            {
                Rules = rules
                Records = records
                Actions = actions
                Communities = communities
            }
        | Failure(msg, _, _) -> failwith msg
