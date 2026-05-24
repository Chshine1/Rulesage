namespace Rulesage.Common.Grammar.Ast

open Rulesage.Common.Grammar

type ArgExpr = { Key: string; Value: PrimitiveExpr }

type ArgBlock = ArgExpr list

type DynamicExpr =
    | Satisfying of ruleId: Identifier * args: ArgBlock
    | ResultOf of actionId: Identifier * args: ArgBlock
    | Node of nodeId: NodeSignature * args: ArgBlock


namespace Rulesage.Common.Grammar.Parsers.Domain

open FParsec
open Rulesage.Common.Grammar
open Rulesage.Common.Grammar.Ast
open Rulesage.Common.Grammar.Parsers.Lexer
open Rulesage.Common.Grammar.Parsers.Primitives

module Dynamic =
    let private pArgExpr: Parser<ArgExpr, ParseContext> =
        pKey .>> skipChar '=' .>>. pPrimitiveExpr
        |>> fun (k, v) -> { Key = k; Value = v }

    let private pArgBlock (keyword: string) : Parser<ArgBlock, ParseContext> =
        opt (skipString keyword >>. sepBy1 pArgExpr (skipChar ','))
        |>> fun ol ->
            match ol with
            | Some l -> l
            | None -> []

    let pDynamicExpr: Parser<DynamicExpr, ParseContext> =
        choice
            [
                skipString "satisfying" >>. pId .>>. (pArgBlock "where")
                |>> DynamicExpr.Satisfying
                skipString "result of" >>. pId .>>. (pArgBlock "where") |>> DynamicExpr.ResultOf
                skipString "node" >>. pNodeId .>>. (pArgBlock "with") |>> DynamicExpr.Node
            ]
