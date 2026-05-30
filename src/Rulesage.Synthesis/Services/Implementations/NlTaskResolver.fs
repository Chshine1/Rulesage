namespace Rulesage.Synthesis.Services.Implementations

open Rulesage.Composition
open Rulesage.Retrieval
open Rulesage.Synthesis.Services.Abstractions

type NlTaskResolver(operationRetrievalService: IRuleRetrievalService, operationComposer: IRuleComposer) =
    interface INlTaskResolver with
        member _.ResolveAsync cancellationToken nlTask =
            operationComposer.ComposeAsync nlTask
