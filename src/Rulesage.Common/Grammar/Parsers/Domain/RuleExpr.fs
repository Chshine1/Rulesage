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

type NodeExpr =
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
    let private pParamExpr: Parser<ParamExpr, ParseContext> =
        pKey .>> skipString "(" .>>. pTypeExpr .>> skipString ")"
        |>> fun (k, t) -> { Key = k; Type = t }

    let private pParamBlock (keyword: string): Parser<ParamExpr list, ParseContext> =
        opt (skipString keyword >>. skipString ":" >>. sepBy1 pParamExpr (skipString ","))
        |>> fun ol ->
            match ol with
            | Some l -> l
            | None -> []
    
    let private pGivenExpr: Parser<GivenExpr, ParseContext> =
        pKey .>> skipString ":" .>>. pValueExpr |>> fun (k, v) -> { Key = k; Value = v }

    let private pGivenBlock: Parser<GivenExpr list, ParseContext> =
        opt (skipString "given" >>. skipString ":" >>. many1 pGivenExpr)
        |>> fun ol ->
            match ol with
            | Some l -> l
            | None -> []
    
    let private pMustBeExpr: Parser<ValueExpr, ParseContext> =
        skipString "must be" >>. skipString ":" >>. pValueExpr

    let pRule: Parser<RuleExpr, ParseContext> =
        pAnnotation .>> skipString "rule"
        .>>. pId
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

    let pNode: Parser<NodeExpr, ParseContext> =
        pAnnotation .>> skipString "node"
        .>>. pId
        .>>. pParamBlock "with"
        |>> fun ((a, i), fs) ->
            {
                Id = i
                Annotation = a
                Fors = fs |> Seq.map (fun f -> f.Key, f) |> Map.ofSeq
            }
    
    let private pReturnsExpr: Parser<TypeExpr, ParseContext> =
        skipString "returns" >>. skipString ":" >>. pTypeExpr

    let pAction: Parser<ActionExpr, ParseContext> =
        pAnnotation .>> skipString "action"
        .>>. pId
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
