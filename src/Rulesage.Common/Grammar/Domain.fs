namespace Rulesage.Common.Grammar

open FParsec
open Rulesage.Common.Grammar.Domain
open Rulesage.Common.Grammar.Parsers
open Rulesage.Common.Grammar.Parsers.Strings
open Rulesage.Common.Grammar.Parsers.Types

type Parser<'a> = Parser<'a, ParseContext>

module Domain =
    let pForExpr: Parser<ForExpr> =
        pKey .>> pstring "(" .>>. pTypeExpr .>> pstring ")"
        |>> fun (k, t) -> { Key = k; Type = t }

    let pForBlock: Parser<ForBlock> =
        opt (pstring "for" >>. pstring ":" >>. sepBy1 pForExpr (pstring ","))
        |>> fun ol ->
            match ol with
            | Some l -> l
            | None -> []

    let pGivenExpr: Parser<GivenExpr> =
        pKey .>> pstring ":" .>>. pValueExpr |>> fun (k, v) -> { Key = k; Value = v }

    let pGivenBlock: Parser<GivenBlock> =
        opt (pstring "given" >>. pstring ":" >>. many1 pGivenExpr)
        |>> fun ol ->
            match ol with
            | Some l -> l
            | None -> []

    let pMustBeExpr: Parser<ValueExpr> =
        pstring "must be" >>. pstring ":" >>. pValueExpr

    let pRule: Parser<RuleExpr> =
        pAnnotation .>> pstring "rule"
        .>>. pId
        .>>. pForBlock
        .>>. pGivenBlock
        .>>. pMustBeExpr
        |>> fun ((((a, i), f), g), m) ->
            {
                Annotation = a
                Id = i
                Fors = f
                Givens = g
                MustBe = m
            }
