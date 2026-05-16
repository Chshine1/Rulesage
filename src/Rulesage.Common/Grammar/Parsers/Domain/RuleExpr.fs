namespace Rulesage.Common.Grammar.Ast

open Rulesage.Common.Grammar

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


namespace Rulesage.Common.Grammar.Parsers.Domain

open FParsec
open Rulesage.Common.Grammar
open Rulesage.Common.Grammar.Ast
open Rulesage.Common.Grammar.Parsers.Domain.Value
open Rulesage.Common.Grammar.Parsers.Lexer
open Rulesage.Common.Grammar.Parsers.Strings
open Rulesage.Common.Grammar.Parsers.Types

module Rule =
    let private pForExpr: Parser<ForExpr, ParseContext> =
        pKey .>> skipString "(" .>>. pTypeExpr .>> skipString ")"
        |>> fun (k, t) -> { Key = k; Type = t }

    let private pForBlock: Parser<ForExpr list, ParseContext> =
        opt (skipString "for" >>. skipString ":" >>. sepBy1 pForExpr (skipString ","))
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
