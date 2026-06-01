namespace Rulesage.Common.Grammar.Ast

open Rulesage.Common.Grammar

type ArgExpr = { Key: string; Value: PrimitiveExpr }

type ArgBlock = ArgExpr list

type DynamicExpr =
    | Satisfying of ruleId: Identifier * args: ArgBlock
    | ResultOf of action: (Identifier * TypeExpr list) * args: ArgBlock
    | Record of record: (Identifier * TypeExpr list) * args: ArgBlock


namespace Rulesage.Common.Grammar.Parsers.Domain

open FParsec
open Rulesage.Common.Grammar
open Rulesage.Common.Grammar.Ast
open Rulesage.Common.Grammar.Parsers.Lexer
open Rulesage.Common.Grammar.Parsers.Primitives
open Rulesage.Common.Grammar.Parsers.Types

module Dynamic =
    let private s = spaces
    let private s1 = spaces1

    let private pArgExpr: Parser<ArgExpr> =
        pKey .>> s .>> skipChar '=' .>> s .>>. pPrimitiveExpr
        |>> fun (k, v) -> { Key = k; Value = v }

    let private pArgBlock (keyword: string) : Parser<ArgBlock> =
        opt (s1 >>. skipString keyword >>. s1 >>. spacedSep1 ',' pArgExpr)
        |>> Option.defaultValue []

    let private genericImpl: Parser<TypeExpr list> =
        between (skipChar '<') (skipChar '>') (s >>. spacedSep1 ',' pTypeExpr .>> s)

    let private pImplId: Parser<string * TypeExpr list> =
        pId .>>. (opt genericImpl |>> Option.defaultValue [])

    let pDynamicExpr: Parser<DynamicExpr> =
        choice
            [
                skipString "interpretation of" >>. s1 >>. pId .>>. (pArgBlock "where")
                |>> DynamicExpr.Satisfying
                skipString "result of" >>. s1 >>. pImplId .>>. (pArgBlock "where")
                |>> DynamicExpr.ResultOf
                skipString "record" >>. s1 >>. pImplId .>>. (pArgBlock "with")
                |>> DynamicExpr.Record
            ]
