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
    | Satisfying of ruleId: Identifier * args: IterArgBlock
    | ResultOf of actionId: Identifier * args: IterArgBlock
    | Record of nodeId: NodeSignature * args: IterArgBlock


namespace Rulesage.Common.Grammar.Parsers.Domain

open FParsec
open Rulesage.Common.Grammar
open Rulesage.Common.Grammar.Ast
open Rulesage.Common.Grammar.Parsers.Lexer
open Rulesage.Common.Grammar.Parsers.Primitives

module Seq =
    let private s = spaces
    let private s1 = spaces1
    
    let private pIterArgExpr: Parser<IterArgExpr, ParseContext> =
        pKey .>> s .>> skipChar '=' .>> s .>>. opt (skipString "iter" .>> s1) .>>. pPrimitiveExpr
        |>> fun ((k, o), v) -> { Key = k; Value = v; Iter = o.IsSome }

    let private pIterArgBlock (keyword: string) : Parser<IterArgBlock, ParseContext> =
        opt (skipString keyword >>. s1 >>. sepBy1 pIterArgExpr (s .>> skipChar ',' .>> s))
        |>> fun ol ->
            match ol with
            | Some l -> l
            | None -> []

    let pSeqExpr: Parser<SeqExpr, ParseContext> =
        skipString "seq" >>. s1
        >>. choice
                [
                    skipString "satisfying" >>. s1 >>. pId .>> s1 .>>. (pIterArgBlock "where")
                    |>> SeqExpr.Satisfying
                    skipString "result of" >>. s1 >>. pId .>> s1 .>>. (pIterArgBlock "where") |>> SeqExpr.ResultOf
                    skipString "record" >>. s1 >>. pRecordId .>> s1 .>>. (pIterArgBlock "with") |>> SeqExpr.Record
                ]
