namespace Rulesage.Synthesis

open Rulesage.Synthesis.Services.Abstractions

type OperationSynthesizer(synthesisUnitFactory: SynthesisUnitFactory, nlTaskResolver: INlTaskResolver) =
    interface IOperationSynthesizer with
        member this.SynthesizeNlTaskAsync(nlTask, cancellationToken) =
            task {
                let! op = nlTask |> nlTaskResolver.ResolveAsync cancellationToken
                let unit = synthesisUnitFactory.Create cancellationToken op Map.empty
                let! result = unit.SynthesizeAsync()
                return result |> Map.toSeq |> readOnlyDict
            }
