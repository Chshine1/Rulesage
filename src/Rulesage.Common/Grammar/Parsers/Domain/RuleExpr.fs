namespace Rulesage.Common.Grammar.Ast

open Rulesage.Common.Grammar

type ParamExpr = { Key: string; Type: TypeExpr }

type GivenExpr = { Key: string; Value: ValueExpr }

type RuleExpr =
    {
        Id: Identifier
        Annotation: string
        Fors: Map<string, ParamExpr>
        Givens: Map<string, GivenExpr>
        MustBe: ValueExpr
    }

type RecordExpr =
    {
        Id: Identifier
        Annotation: string
        Fors: Map<string, ParamExpr>
    }

type ActionExpr =
    {
        Id: Identifier
        Annotation: string
        Fors: Map<string, ParamExpr>
        Returns: TypeExpr
        Script: string
    }

namespace Rulesage.Common.Grammar.Parsers.Domain

open FParsec
open Rulesage.Common.Grammar
open Rulesage.Common.Grammar.Ast
open Rulesage.Common.Grammar.Parsers.Domain.Value
open Rulesage.Common.Grammar.Parsers.Lexer
open Rulesage.Common.Grammar.Parsers.Strings
open Rulesage.Common.Grammar.Parsers.Types

module Rule =
    let private s = spaces
    let private s1 = spaces1

    let private pParamExpr: Parser<ParamExpr> =
        pKey .>> s .>> skipChar '(' .>> s .>>. pTypeExpr .>> s .>> skipChar ')'
        |>> fun (k, t) -> { Key = k; Type = t }

    let private pParamBlock (keyword: string) : Parser<ParamExpr list> =
        opt (
            skipString keyword >>. s1 >>. sepBy1 pParamExpr (s .>> skipChar ',' .>> s)
            .>> s1
        )
        |>> fun ol ->
            match ol with
            | Some l -> l
            | None -> []

    let private pGivenExpr: Parser<GivenExpr> =
        pKey .>> s .>> skipChar ':' .>> s .>>. pValueExpr
        |>> fun (k, v) -> { Key = k; Value = v }

    let private pGivenBlock: Parser<GivenExpr list> =
        opt (skipString "given" >>. s >>. skipChar ':' >>. s >>. sepBy1 pGivenExpr s1 .>> s1)
        |>> fun ol ->
            match ol with
            | Some l -> l
            | None -> []

    let private pMustBeExpr: Parser<ValueExpr> =
        skipString "must be" >>. s >>. skipChar ':' >>. s >>. pValueExpr

    let pRule: Parser<RuleExpr> =
        pAnnotation .>> s .>> skipString "rule" .>> s1
        .>>. (pId .>> s1)
        .>>. pParamBlock "for"
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

    let pRecord: Parser<RecordExpr> =
        pAnnotation .>> s .>> skipString "record" .>> s1
        .>>. (pId .>> s1)
        .>>. pParamBlock "with"
        |>> fun ((a, i), fs) ->
            {
                Id = i
                Annotation = a
                Fors = fs |> Seq.map (fun f -> f.Key, f) |> Map.ofSeq
            }

    let private pReturnsExpr: Parser<TypeExpr> =
        skipString "returns" >>. s >>. skipChar ':' >>. s >>. pTypeExpr

    let pAction: Parser<ActionExpr> =
        pAnnotation .>> s .>> skipString "action" .>> s1
        .>>. (pId .>> s1)
        .>>. pParamBlock "on"
        .>>. pReturnsExpr
        |>> fun (((a, i), fs), r) ->
            {
                Id = i
                Annotation = a
                Fors = fs |> Seq.map (fun f -> f.Key, f) |> Map.ofSeq
                Returns = r
                Script = ""
            }
