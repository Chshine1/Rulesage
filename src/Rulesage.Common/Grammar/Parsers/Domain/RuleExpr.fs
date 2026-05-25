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
        GenericParams: string seq
        Fors: Map<string, ParamExpr>
    }

type ActionExpr =
    {
        Id: Identifier
        Annotation: string
        GenericParams: string seq
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
open Rulesage.Common.Grammar.Parsers.Types

module Rule =
    let private s = spaces
    let private s1 = spaces1

    let private pParamExpr: Parser<ParamExpr> =
        pKey .>> s .>>. between (skipChar '(') (skipChar ')') (s >>. pTypeExpr .>> s)
        |>> fun (k, t) -> { Key = k; Type = t }

    let private pParamBlock keyword =
        s1 >>. optKeywordBlock keyword pParamExpr

    let private pGivenExpr: Parser<GivenExpr> =
        pKey .>> s .>> skipChar ':' .>> s .>>. pValueExpr
        |>> fun (k, v) -> { Key = k; Value = v }

    let private pGivenBlock: Parser<GivenExpr list> =
        opt (s1 >>. skipString "given" >>. s >>. skipChar ':' >>. s >>. sepBy1 pGivenExpr s1)
        |>> Option.defaultValue []

    let private pMustBeExpr: Parser<ValueExpr> =
        skipString "must be" >>. s >>. skipChar ':' >>. s >>. pValueExpr

    let pRule (annotation: string) : Parser<RuleExpr> =
        skipString "rule" .>> s1 >>. pId
        .>>. pParamBlock "for"
        .>>. pGivenBlock
        .>>. pMustBeExpr
        |>> fun (((i, fs), gs), m) ->
            {
                Id = i
                Annotation = annotation
                Fors = fs |> Seq.map (fun f -> f.Key, f) |> Map.ofSeq
                Givens = gs |> Seq.map (fun g -> g.Key, g) |> Map.ofSeq
                MustBe = m
            }

    let pRecord (annotation: string) : Parser<RecordExpr> =
        skipString "record" .>> s1 >>. pGenericId .>>. pParamBlock "with"
        |>> fun ((i, gs), fs) ->
            {
                Id = i
                Annotation = annotation
                GenericParams = gs
                Fors = fs |> Seq.map (fun f -> f.Key, f) |> Map.ofSeq
            }

    let private pReturnsExpr: Parser<TypeExpr> =
        s1 >>. skipString "returns" >>. s >>. pTypeExpr

    let pAction (annotation: string) : Parser<ActionExpr> =
        skipString "action" .>> s1 >>. pGenericId
        .>>. pParamBlock "on"
        .>>. pReturnsExpr
        |>> fun (((i, gs), fs), r) ->
            {
                Id = i
                Annotation = annotation
                GenericParams = gs
                Fors = fs |> Seq.map (fun f -> f.Key, f) |> Map.ofSeq
                Returns = r
                Script = ""
            }
