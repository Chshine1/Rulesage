namespace Rulesage.Common.Utils

open System.Threading
open System.Threading.Tasks

module TaskUtils =
    let whenAll (cts: CancellationTokenSource) (tasks: seq<Task<'T>>) =
        task {
            try
                return! Task.WhenAll(tasks)
            with ex ->
                cts.Cancel()
                return! Task.FromException<'T[]>(ex)
        }