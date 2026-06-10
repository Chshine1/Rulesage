namespace Rulesage.Synthesis.Services.Implementations

open Rulesage.Composition
open Rulesage.Synthesis.Services.Abstractions

type SubjectResolver(operationComposer: IRuleComposer) =
    interface ISubjectResolver with
        member _.ResolveAsync cancellationToken nlTask =
            operationComposer.ComposeAsync(nlTask, cancellationToken)

        member _.ResolveWithConstraintAsync cancellationToken expectedType nlTask =
            operationComposer.ComposeWithConstrainAsync(nlTask, expectedType, "", cancellationToken)
