namespace Rulesage.Common.Grammar.Ast

type ActionExpr =
    {
        Header: UnitHeader
        Returns: TypeExpr
        Script: string
    }

namespace Rulesage.Common.Grammar.Parsers.Domain

open FParsec
open Rulesage.Common.Grammar
open Rulesage.Common.Grammar.Ast
open Rulesage.Common.Grammar.Parsers.Domain.Unit
open Rulesage.Common.Grammar.Parsers.Types

module Action =
    let private s = spaces
    let private s1 = spaces1

    let private pReturnsExpr: Parser<TypeExpr> =
        s1 >>. skipString "returns" >>. s >>. pTypeExpr

    let pAction (domain: string) : Parser<ActionExpr> =
        pHeader domain "action" "on" .>>. pReturnsExpr
        |>> fun (h, r) -> { Header = h; Returns = r; Script = "" }
