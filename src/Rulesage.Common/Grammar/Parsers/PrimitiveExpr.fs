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
        skipString "ref" >>. between (skipChar '(') (skipChar ')') Types.pTypeExpr
        .>>. Strings.pSingleLineString
        |>> fun (t, s) -> { ExpctedType = t; Desc = s }

    let pPrimitiveExpr, private pPrimitiveExprRef =
        createParserForwardedToRef<PrimitiveExpr, ParseContext> ()

    let private pArrayExpr: Parser<PrimitiveExpr, ParseContext> =
        between (skipChar '[') (skipChar ']') (sepBy pPrimitiveExpr (skipChar ','))
        |>> PrimitiveExpr.Array

    pPrimitiveExprRef.Value <-
        choice
            [
                Strings.pSingleLineString |>> PrimitiveExpr.StringLiteral
                pRef |>> PrimitiveExpr.Ref
                Vars.pVarExpr |>> PrimitiveExpr.Var
                pArrayExpr
            ]
