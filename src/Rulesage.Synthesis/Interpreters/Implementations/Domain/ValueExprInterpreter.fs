namespace Rulesage.Synthesis.Interpreters.Implementations.Domain

open Rulesage.Common.Grammar.Ast
open Rulesage.Synthesis.Interpreters.Abstractions

type ValueExprInterpreter
    (
        primitiveItp: IExprInterpreter<PrimitiveExpr>,
        dynamicItp: IExprInterpreter<DynamicExpr>,
        seqItp: IExprInterpreter<SeqExpr>
    ) =
    interface IExprInterpreter<ValueExpr> with
        member _.InterpretAsync ctx expr =
            match expr with
            | ValueExpr.Primitive e -> primitiveItp.InterpretAsync ctx e
            | ValueExpr.Dynamic d -> dynamicItp.InterpretAsync ctx d
            | ValueExpr.Seq s -> seqItp.InterpretAsync ctx s
