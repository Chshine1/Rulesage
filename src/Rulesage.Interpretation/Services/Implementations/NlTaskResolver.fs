namespace Rulesage.Synthesis.Services.Implementations

open Rulesage.Composition
open Rulesage.Synthesis.Services.Abstractions

type NlTaskResolver(operationComposer: IRuleComposer) =
    interface ISubjectResolver with
        member _.ResolveAsync cancellationToken nlTask =
            operationComposer.ComposeAsync(nlTask, cancellationToken)
