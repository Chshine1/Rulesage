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
    | Node of nodeId: NodeSignature * args: IterArgBlock


namespace Rulesage.Common.Grammar.Parsers.Domain

open FParsec
open Rulesage.Common.Grammar
open Rulesage.Common.Grammar.Ast
open Rulesage.Common.Grammar.Parsers.Lexer
open Rulesage.Common.Grammar.Parsers.Primitives

module Seq =
    let private pIterArgExpr: Parser<IterArgExpr, ParseContext> =
        pKey .>> skipString "=" .>>. opt (skipString "iter") .>>. pPrimitiveExpr
        |>> fun ((k, o), v) -> { Key = k; Value = v; Iter = o.IsSome }

    let private pIterArgBlock (keyword: string) : Parser<IterArgBlock, ParseContext> =
        opt (skipString keyword >>. sepBy1 pIterArgExpr (skipString ","))
        |>> fun ol ->
            match ol with
            | Some l -> l
            | None -> []

    let pSeqExpr: Parser<SeqExpr, ParseContext> =
        skipString "seq"
        >>. choice
                [
                    skipString "satisfying" >>. pId .>>. (pIterArgBlock "where")
                    |>> SeqExpr.Satisfying
                    skipString "result of" >>. pId .>>. (pIterArgBlock "where") |>> SeqExpr.ResultOf
                    skipString "node" >>. pNodeId .>>. (pIterArgBlock "with") |>> SeqExpr.Node
                ]
