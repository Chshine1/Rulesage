namespace Rulesage.Common.Grammar.Ast

open Rulesage.Common.Grammar

type ArgExpr = { Key: string; Value: PrimitiveExpr }

type ArgBlock = ArgExpr list

type DynamicExpr =
    | Satisfying of ruleId: Identifier * args: ArgBlock
    | ResultOf of actionId: Identifier * args: ArgBlock
    | Record of nodeId: Identifier * args: ArgBlock


namespace Rulesage.Common.Grammar.Parsers.Domain

open FParsec
open Rulesage.Common.Grammar
open Rulesage.Common.Grammar.Ast
open Rulesage.Common.Grammar.Parsers.Lexer
open Rulesage.Common.Grammar.Parsers.Primitives

module Dynamic =
    let private s = spaces
    let private s1 = spaces1

    let private pArgExpr: Parser<ArgExpr> =
        pKey .>> s .>> skipChar '=' .>> s .>>. pPrimitiveExpr
        |>> fun (k, v) -> { Key = k; Value = v }

    let private pArgBlock (keyword: string) : Parser<ArgBlock> =
        opt (skipString keyword >>. s1 >>. sepBy1 pArgExpr (s .>> skipChar ',' .>> s))
        |>> fun ol ->
            match ol with
            | Some l -> l
            | None -> []

    let pDynamicExpr: Parser<DynamicExpr> =
        choice
            [
                skipString "satisfying" >>. s1 >>. pId .>> s1 .>>. (pArgBlock "where")
                |>> DynamicExpr.Satisfying
                skipString "result of" >>. s1 >>. pId .>> s1 .>>. (pArgBlock "where")
                |>> DynamicExpr.ResultOf
                skipString "record" >>. s1 >>. pId .>> s1 .>>. (pArgBlock "with")
                |>> DynamicExpr.Record
            ]
