namespace Rulesage.Synthesis.Services.Implementations

open Rulesage.Composition
open Rulesage.Retrieval
open Rulesage.Synthesis.Services.Abstractions

type NlTaskResolver(operationRetrievalService: IRuleRetrievalService, operationComposer: IRuleComposer) =
    interface INlTaskResolver with
        member _.ResolveAsync cancellationToken nlTask =
            task {
                let! prefetchedOps =
                    operationRetrievalService.RetrieveAsync(nlTask, System.Nullable(), cancellationToken)

                return! operationComposer.ComposeAsync(nlTask, prefetchedOps, cancellationToken)
            }
