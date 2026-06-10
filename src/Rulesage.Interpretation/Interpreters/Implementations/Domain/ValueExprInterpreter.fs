namespace Rulesage.Synthesis.Interpreters.Implementations.Domain

open System.Threading.Tasks
open Rulesage.Common.Grammar.Ast
open Rulesage.Synthesis
open Rulesage.Synthesis.Interpreters.Abstractions
open Rulesage.Synthesis.Types

type ValueExprInterpreter
    (
        primitiveItp: IExprInterpreter<PrimitiveExpr>,
        dynamicItp: IExprInterpreter<DynamicExpr>,
        seqItp: IExprInterpreter<SeqExpr>
    ) =

    interface IExprInterpreter<ValueExpr> with
        member _.InterpretAsync (ctx: SynthesisContext) (expr: ValueExpr) : Task<InterpretedValue> =
            let branches, elseBody = expr

            let interpretBody (body: BodyExpr) =
                match body with
                | BodyExpr.Primitive e -> primitiveItp.InterpretAsync ctx e
                | BodyExpr.Dynamic d -> dynamicItp.InterpretAsync ctx d
                | BodyExpr.Seq s -> seqItp.InterpretAsync ctx s

            let rec evalCondition (cond: ConditionExpr) =
                task {
                    match cond with
                    | IsTest(left, negated, right) ->
                        let! leftVal = primitiveItp.InterpretAsync ctx left
                        let! rightVal = primitiveItp.InterpretAsync ctx right
                        let equal = (leftVal = rightVal)
                        return if negated then not equal else equal

                    | And(l, r) ->
                        let! lr = evalCondition l
                        if lr then return! evalCondition r else return false

                    | Or(l, r) ->
                        let! lr = evalCondition l
                        if lr then return true else return! evalCondition r
                }

            let rec loop remainingBranches =
                task {
                    match remainingBranches with
                    | [] -> return! interpretBody elseBody
                    | (cond, body) :: rest ->
                        let! condTrue = evalCondition cond

                        if condTrue then
                            return! interpretBody body
                        else
                            return! loop rest
                }

            loop branches
