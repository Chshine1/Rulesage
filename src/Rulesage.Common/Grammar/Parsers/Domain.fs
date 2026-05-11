namespace Rulesage.Common.Grammar.Ast

open Rulesage.Common.Grammar

type ArgExpr = { Key: string; Value: PrimitiveExpr }

type ArgBlock = ArgExpr list

type DynamicExpr =
    | Satisfying of ruleId: Identifier * args: ArgBlock
    | ResultOf of actionId: Identifier * args: ArgBlock
    | Node of nodeId: NodeSignature * args: ArgBlock

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

type ValueExpr =
    | Primitive of expr: PrimitiveExpr
    | Dynamic of expr: DynamicExpr
    | Seq of expr: SeqExpr

type ForExpr = { Key: string; Type: TypeExpr }

type GivenExpr = { Key: string; Value: ValueExpr }

type RuleExpr =
    {
        Id: Identifier
        Annotation: string
        Fors: Map<string, ForExpr>
        Givens: Map<string, GivenExpr>
        MustBe: ValueExpr
    }


namespace Rulesage.Common.Grammar.Parsers

open FParsec
open Rulesage.Common.Grammar
open Rulesage.Common.Grammar.Ast
open Rulesage.Common.Grammar.Parsers.Lexer
open Rulesage.Common.Grammar.Parsers.Strings
open Rulesage.Common.Grammar.Parsers.Types

module Domain =
    let private pArgExpr: Parser<ArgExpr, ParseContext> =
        pKey .>> skipString "=" .>>. Primitives.pPrimitiveExpr
        |>> fun (k, v) -> { Key = k; Value = v }

    let pArgBlock (keyword: string) : Parser<ArgBlock, ParseContext> =
        opt (skipString keyword >>. sepBy1 pArgExpr (skipString ","))
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

    let private pIterArgExpr: Parser<IterArgExpr, ParseContext> =
        pKey .>> skipString "="
        .>>. opt (skipString "iter")
        .>>. Primitives.pPrimitiveExpr
        |>> fun ((k, o), v) -> { Key = k; Value = v; Iter = o.IsSome }

    let pIterArgBlock (keyword: string) : Parser<IterArgBlock, ParseContext> =
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

    let pValueExpr: Parser<ValueExpr, ParseContext> =
        choice
            [
                Primitives.pPrimitiveExpr |>> ValueExpr.Primitive
                pDynamicExpr |>> ValueExpr.Dynamic
                pSeqExpr |>> ValueExpr.Seq
            ]

    let pForExpr: Parser<ForExpr, ParseContext> =
        pKey .>> skipString "(" .>>. pTypeExpr .>> skipString ")"
        |>> fun (k, t) -> { Key = k; Type = t }

    let pForBlock: Parser<ForExpr list, ParseContext> =
        opt (skipString "for" >>. skipString ":" >>. sepBy1 pForExpr (skipString ","))
        |>> fun ol ->
            match ol with
            | Some l -> l
            | None -> []

    let pGivenExpr: Parser<GivenExpr, ParseContext> =
        pKey .>> skipString ":" .>>. pValueExpr |>> fun (k, v) -> { Key = k; Value = v }

    let pGivenBlock: Parser<GivenExpr list, ParseContext> =
        opt (skipString "given" >>. skipString ":" >>. many1 pGivenExpr)
        |>> fun ol ->
            match ol with
            | Some l -> l
            | None -> []

    let pMustBeExpr: Parser<ValueExpr, ParseContext> =
        skipString "must be" >>. skipString ":" >>. pValueExpr

    let pRule: Parser<RuleExpr, ParseContext> =
        pAnnotation .>> skipString "rule"
        .>>. pId
        .>>. pForBlock
        .>>. pGivenBlock
        .>>. pMustBeExpr
        |>> fun ((((a, i), fs), gs), m) ->
            {
                Id = i
                Annotation = a
                Fors = fs |> Seq.map (fun f -> f.Key, f) |> Map.ofSeq
                Givens = gs |> Seq.map (fun g -> g.Key, g) |> Map.ofSeq
                MustBe = m
            }
