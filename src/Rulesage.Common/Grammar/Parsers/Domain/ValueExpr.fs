namespace Rulesage.Common.Grammar.Ast

type ConditionExpr =
    | IsTest of left: PrimitiveExpr * negated: bool * right: PrimitiveExpr
    | And of ConditionExpr * ConditionExpr
    | Or of ConditionExpr * ConditionExpr

type BodyExpr =
    | Primitive of expr: PrimitiveExpr
    | Dynamic of expr: DynamicExpr
    | Seq of expr: SeqExpr

type ValueExpr = (ConditionExpr * BodyExpr) list * BodyExpr


namespace Rulesage.Common.Grammar.Parsers.Domain

open FParsec
open Rulesage.Common.Grammar
open Rulesage.Common.Grammar.Ast
open Rulesage.Common.Grammar.Parsers.Domain.Dynamic
open Rulesage.Common.Grammar.Parsers.Domain.Seq
open Rulesage.Common.Grammar.Parsers.Primitives

module Value =
    let private s = spaces
    let private s1 = spaces1

    let private pBodyExpr: Parser<BodyExpr> =
        choice
            [
                pPrimitiveExpr |>> BodyExpr.Primitive
                pDynamicExpr |>> BodyExpr.Dynamic
                pSeqExpr |>> BodyExpr.Seq
            ]

    let private pIsTest =
        pPrimitiveExpr .>> s1
        .>>. (skipString "is" >>. opt (s1 .>> skipString "not") .>> s1 .>>. pPrimitiveExpr)
        |>> fun (left, (negated, right)) -> IsTest(left, negated.IsSome, right)

    let private opp = OperatorPrecedenceParser<ConditionExpr, unit, unit>()
    let private pCondExpr = opp.ExpressionParser

    let private pParenCond = between (skipChar '(' >>. s) (skipChar ')' >>. s) pCondExpr

    opp.TermParser <- pIsTest <|> pParenCond
    opp.AddOperator(InfixOperator("and", s1, 1, Associativity.Left, fun l r -> And(l, r)))
    opp.AddOperator(InfixOperator("or", s1, 2, Associativity.Left, fun l r -> Or(l, r)))

    let pIfBranch: Parser<ConditionExpr * BodyExpr> =
        skipString "if" >>. s1 >>. pCondExpr .>> s1 .>> skipString "then" .>> s1
        .>>. pBodyExpr

    let pElseIfBranch: Parser<ConditionExpr * BodyExpr> =
        skipString "else" >>. s1 >>. skipString "if" >>. s1 >>. pCondExpr
        .>> s1
        .>> skipString "then"
        .>> s1
        .>>. pBodyExpr

    let pValueExpr: Parser<ValueExpr> =
        opt (
            pIfBranch .>>. many (attempt (s1 >>. pElseIfBranch))
            .>> s1
            .>> skipString "else"
            .>> s1
        )
        .>>. pBodyExpr
        |>> fun (o, elseVal) ->
            match o with
            | Some(firstBranch, restBranches) -> ValueExpr(firstBranch :: restBranches, elseVal)
            | None -> ValueExpr([], elseVal)
