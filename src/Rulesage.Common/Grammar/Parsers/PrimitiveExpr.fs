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
open Rulesage.Common.Grammar.Parsers.Strings
open Rulesage.Common.Grammar.Parsers.Types
open Rulesage.Common.Grammar.Parsers.Vars

module Primitives =
    let private s = spaces
    let private s1 = spaces1

    let pRef: Parser<RefExpr, ParseContext> =
        skipString "ref"
        >>. s
        >>. between (skipChar '(') (skipChar ')') (s >>. pTypeExpr .>> s)
        .>> s1
        .>>. pSingleLineString
        |>> fun (t, s) -> { ExpctedType = t; Desc = s }

    let pPrimitiveExpr, private pPrimitiveExprRef =
        createParserForwardedToRef<PrimitiveExpr, ParseContext> ()

    let private pArrayExpr: Parser<PrimitiveExpr, ParseContext> =
        between (skipChar '[') (skipChar ']') (s >>. sepBy pPrimitiveExpr (s >>. skipChar ',' >>. s) .>> s)
        |>> PrimitiveExpr.Array

    pPrimitiveExprRef.Value <-
        choice
            [
                pSingleLineString |>> PrimitiveExpr.StringLiteral
                pRef |>> PrimitiveExpr.Ref
                pVarExpr |>> PrimitiveExpr.Var
                pArrayExpr
            ]
