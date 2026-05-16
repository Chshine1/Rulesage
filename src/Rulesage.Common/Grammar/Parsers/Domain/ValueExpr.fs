namespace Rulesage.Common.Grammar.Ast

type ValueExpr =
    | Primitive of expr: PrimitiveExpr
    | Dynamic of expr: DynamicExpr
    | Seq of expr: SeqExpr


namespace Rulesage.Common.Grammar.Parsers.Domain

open FParsec
open Rulesage.Common.Grammar
open Rulesage.Common.Grammar.Ast
open Rulesage.Common.Grammar.Parsers.Domain.Dynamic
open Rulesage.Common.Grammar.Parsers.Domain.Seq
open Rulesage.Common.Grammar.Parsers.Primitives

module Value =
    let pValueExpr: Parser<ValueExpr, ParseContext> =
        choice
            [
                pPrimitiveExpr |>> ValueExpr.Primitive
                pDynamicExpr |>> ValueExpr.Dynamic
                pSeqExpr |>> ValueExpr.Seq
            ]
