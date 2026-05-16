namespace Rulesage.Synthesis.Interpreters.Implementations

open System.Text.Json
open System.Threading.Tasks
open Rulesage.Common.Grammar.Ast
open Rulesage.Common.Utils.TaskUtils
open Rulesage.Synthesis.Interpreters.Abstractions
open Rulesage.Synthesis.Types

type StringTemplateInterpreter(varItp: IExprInterpreter<VarExpr>, jsonOptions: JsonSerializerOptions) =
    interface IExprInterpreter<StringTemplate> with
        member _.InterpretAsync ctx expr =
            task {
                let parts =
                    expr
                    |> Seq.map (fun p ->
                        match p with
                        | StringPart.Literal l -> Task.FromResult l
                        | StringPart.Interpolation i ->
                            task {
                                let! syn = varItp.InterpretAsync ctx i
                                return JsonSerializer.Serialize(syn, jsonOptions)
                            }
                    )

                let! result = parts |> whenAll ctx.CtSource
                return SynthesizedValue.Leaf(result |> String.concat "")
            }
