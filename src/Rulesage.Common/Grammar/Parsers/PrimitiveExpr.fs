namespace Rulesage.Common.Grammar.Ast

type RefExpr =
    {
        ExpctedType: TypeExpr
        Desc: StringTemplate
    }

type PrimitiveExpr =
    | StringLiteral of parts: StringTemplate
    | Var of expr: VarExpr
    | Ref of expr: RefExpr
    | Array of arr: PrimitiveExpr list

namespace Rulesage.Common.Grammar.Parsers

open FParsec
open Rulesage.Common.Grammar
open Rulesage.Common.Grammar.Ast

module Primitives =
    let pRef: Parser<RefExpr, ParseContext> =
        skipString "ref" >>. between (skipString "(") (skipString ")") Types.pTypeExpr
        .>>. Strings.pSingleLineString
        |>> fun (t, s) -> { ExpctedType = t; Desc = s }

    let pPrimitiveExpr, private pPrimitiveExprRef =
        createParserForwardedToRef<PrimitiveExpr, ParseContext> ()

    let private pArrayExpr: Parser<PrimitiveExpr, ParseContext> =
        between (skipString "[") (skipString "]") (sepBy pPrimitiveExpr (skipString ","))
        |>> PrimitiveExpr.Array

    pPrimitiveExprRef.Value <-
        choice
            [
                Strings.pSingleLineString |>> PrimitiveExpr.StringLiteral
                pRef |>> PrimitiveExpr.Ref
                Vars.pVarExpr |>> PrimitiveExpr.Var
                pArrayExpr
            ]
