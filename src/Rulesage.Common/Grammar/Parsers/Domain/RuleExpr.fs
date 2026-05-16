namespace Rulesage.Common.Grammar.Ast

open Rulesage.Common.Grammar

type RuleExpr =
    {
        Id: Identifier
        Annotation: string
        Fors: Map<string, ForItem>
        Givens: Map<string, GivenItem>
        MustBe: ValueExpr
    }


namespace Rulesage.Common.Grammar.Parsers.Domain

open FParsec
open Rulesage.Common.Grammar
open Rulesage.Common.Grammar.Ast
open Rulesage.Common.Grammar.Parsers.Domain.For
open Rulesage.Common.Grammar.Parsers.Domain.Given
open Rulesage.Common.Grammar.Parsers.Domain.Value
open Rulesage.Common.Grammar.Parsers.Lexer
open Rulesage.Common.Grammar.Parsers.Strings

module Rule =
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
