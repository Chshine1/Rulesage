namespace Rulesage.Common.Grammar

type AtomicType =
    | Literal
    | Node of id: string

type TypeExpr = { Atomic: AtomicType; Dimension: int }

type VarSource =
    | For
    | Given

type VarExpr =
    {
        Source: VarSource
        Key: string
        Fields: string list
    }

type StringPart =
    | Literal of string
    | Interpolation of var: VarExpr

type RefExpr =
    {
        ExpctedType: TypeExpr
        Desc: StringPart list
    }

type PrimitiveExpr =
    | StringLiteral of parts: StringPart list
    | Var of expr: VarExpr
    | Ref of expr: RefExpr
    | Array of arr: PrimitiveExpr list

type ArgExpr = { Key: string; Value: PrimitiveExpr }

type ArgBlock = ArgExpr list

type DynamicExpr =
    | Satisfying of ruleId: Identifier * args: ArgBlock
    | ResultOf of actionId: Identifier * args: ArgBlock
    | Node of nodeId: NodeSignature * args: ArgBlock

type IterArgExpr =
    {
        Key: string
        Value: PrimitiveExpr
        Iter: bool
    }

type IterArgBlock = IterArgExpr list

type SeqExpr =
    | Satisfying of ruleId: Identifier * args: IterArgBlock
    | ResultOf of actionId: Identifier * args: IterArgBlock
    | Node of nodeId: NodeSignature * args: IterArgBlock

type ValueExpr =
    | Primitive of expr: PrimitiveExpr
    | Dynamic of expr: DynamicExpr
    | Seq of expr: SeqExpr

namespace Rulesage.Common.Grammar.Domain

open Rulesage.Common.Grammar

type ForExpr = { Key: string; Type: TypeExpr }

type ForBlock = ForExpr list

type GivenExpr = { Key: string; Value: ValueExpr }

type GivenBlock = GivenExpr list

type RuleExpr =
    {
        Annotation: string
        Id: Identifier
        Fors: ForBlock
        Givens: GivenBlock
        MustBe: ValueExpr
    }
