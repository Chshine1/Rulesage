namespace Rulesage.Synthesis.Services.Abstractions

open System.Threading
open System.Threading.Tasks
open Rulesage.Common.Types.Domain

type INlTaskResolver =
    abstract member ResolveAsync: cancellationToken: CancellationToken -> nlTask: string -> Task<OperationBlueprint>
