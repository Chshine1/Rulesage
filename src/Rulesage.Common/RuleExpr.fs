namespace Rulesage.Common.Grammar.Ast

type GivenExpr = { Key: string; Value: ValueExpr }

type RuleExpr =
    {
        Header: UnitHeader
        Givens: Map<string, GivenExpr>
    }

namespace Rulesage.Common.Grammar.Parsers.Domain

open FParsec
open Rulesage.Common.Grammar
open Rulesage.Common.Grammar.Ast
open Rulesage.Common.Grammar.Parsers.Domain.Unit
open Rulesage.Common.Grammar.Parsers.Domain.Value
open Rulesage.Common.Grammar.Parsers.Lexer

module Rule =
    let private s = spaces
    let private s1 = spaces1

    let private pGivenExpr: Parser<GivenExpr> =
        pKey .>> s .>> skipChar ':' .>> s .>>. pValueExpr
        |>> fun (k, v) -> { Key = k; Value = v }

    let private pGivenBlock: Parser<GivenExpr list> =
        opt (
            (attempt (s1 >>. skipString "given"))
            >>. s
            >>. skipChar ':'
            >>. s
            >>. (pGivenExpr .>>. many (attempt (s1 >>. pGivenExpr)))
            |>> (fun (n, l) -> n :: l)
        )
        |>> Option.defaultValue []

    let pRule (domain: string) : Parser<RuleExpr> =
        pHeader domain "rule" "for" .>>. pGivenBlock
        |>> fun (h, gs) ->
            {
                Header = h
                Givens = gs |> Seq.map (fun g -> g.Key, g) |> Map.ofSeq
            }
