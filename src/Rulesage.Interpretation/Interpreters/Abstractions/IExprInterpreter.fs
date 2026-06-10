namespace Rulesage.Synthesis.Interpreters.Abstractions

open System.Threading.Tasks
open Rulesage.Synthesis
open Rulesage.Synthesis.Types

type IExprInterpreter<'TExpr> =
    abstract InterpretAsync: ctx: SynthesisContext -> expr: 'TExpr -> Task<InterpretedValue>
