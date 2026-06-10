namespace Rulesage.Common.Grammar.Ast

open Rulesage.Common.Grammar

type IterArgExpr =
    {
        Key: string
        Value: PrimitiveExpr
        Iter: bool
    }

type IterArgBlock = IterArgExpr list

type SeqExpr =
    | Concept of closedConcept: (Identifier * TypeExpr list) * args: IterArgBlock
    | ResultOf of closedAction: (Identifier * TypeExpr list) * args: IterArgBlock
    | InterpretationOf of closedRule: (Identifier * TypeExpr list) * args: IterArgBlock

namespace Rulesage.Common.Grammar.Parsers.Domain

open FParsec
open Rulesage.Common.Grammar
open Rulesage.Common.Grammar.Ast
open Rulesage.Common.Grammar.Parsers.Lexer
open Rulesage.Common.Grammar.Parsers.Primitives
open Rulesage.Common.Grammar.Parsers.Types

module Seq =
    let private s = spaces
    let private s1 = spaces1

    let private pIterArgExpr: Parser<IterArgExpr> =
        pKey .>> s .>> skipChar '=' .>> s
        .>>. opt (skipString "iter" .>> s1)
        .>>. pPrimitiveExpr
        |>> fun ((k, o), v) -> { Key = k; Value = v; Iter = o.IsSome }

    let private pIterArgBlock (keyword: string) : Parser<IterArgBlock> =
        opt (s1 >>. skipString keyword >>. s1 >>. spacedSep1 ',' pIterArgExpr)
        |>> fun ol ->
            match ol with
            | Some l -> l
            | None -> []

    let private genericImpl: Parser<TypeExpr list> =
        between (skipChar '<') (skipChar '>') (s >>. spacedSep1 ',' pTypeExpr .>> s)

    let private pImplId: Parser<string * TypeExpr list> =
        pId .>>. (opt genericImpl |>> Option.defaultValue [])

    let pSeqExpr: Parser<SeqExpr> =
        skipString "seq"
        >>. s1
        >>. choice
                [
                    skipString "concept" >>. s1 >>. pImplId .>>. (pIterArgBlock "with")
                    |>> SeqExpr.Concept
                    skipString "result of" >>. s1 >>. pImplId .>>. (pIterArgBlock "where")
                    |>> SeqExpr.ResultOf
                    skipString "interpretation of" >>. s1 >>. pImplId .>>. (pIterArgBlock "where")
                    |>> SeqExpr.InterpretationOf
                ]
