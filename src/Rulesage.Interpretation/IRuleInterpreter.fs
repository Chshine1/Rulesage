namespace Rulesage.Synthesis

open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks
open Rulesage.Synthesis.Types

type IRuleInterpreter =
    abstract member InterpretSubjectAsync:
        subject: string * [<Optional; DefaultParameterValue(CancellationToken())>] cancellationToken: CancellationToken ->
            Task<InterpretedValue>
