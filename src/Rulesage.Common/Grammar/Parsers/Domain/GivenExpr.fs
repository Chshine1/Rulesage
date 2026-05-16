namespace Rulesage.Common.Grammar.Ast

type GivenItem = { Key: string; Value: ValueExpr }

type GivenExpr = GivenItem list


namespace Rulesage.Common.Grammar.Parsers.Domain

open FParsec
open Rulesage.Common.Grammar
open Rulesage.Common.Grammar.Ast
open Rulesage.Common.Grammar.Parsers.Domain.Value
open Rulesage.Common.Grammar.Parsers.Lexer

module Given =
    let pGivenExpr: Parser<GivenItem, ParseContext> =
        pKey .>> skipString ":" .>>. pValueExpr |>> fun (k, v) -> { Key = k; Value = v }

    let pGivenBlock: Parser<GivenItem list, ParseContext> =
        opt (skipString "given" >>. skipString ":" >>. many1 pGivenExpr)
        |>> fun ol ->
            match ol with
            | Some l -> l
            | None -> []
