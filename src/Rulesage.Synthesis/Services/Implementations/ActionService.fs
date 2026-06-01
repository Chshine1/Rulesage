namespace Rulesage.Synthesis.Services.Implementations

open System.Linq
open System.Threading
open System.Threading.Tasks
open Rulesage.Common.Grammar
open Rulesage.Common.Grammar.Ast
open Rulesage.Common.Repositories.Abstractions
open Rulesage.Synthesis.Services.Abstractions
open MoonSharp.Interpreter
open Rulesage.Synthesis.Types

type ActionService(actionRepository: IActionRepository) =
    let actionsCache: Map<Identifier, ActionExpr> = Map.empty
    
    let findActionByIdAsync (cancellationToken: CancellationToken) (actionId: Identifier): Task<ActionExpr> =
        task {
            let oa = actionsCache |> Map.tryFind actionId
            match oa with
            | Some a -> return a
            | None ->
                let! result = actionRepository.FindByIdsAsync([actionId], cancellationToken)
                return result.First()
        }
    
    interface IActionService with
        member _.CallAsync cancellationToken actionId args =
            task {
                let! action = findActionByIdAsync cancellationToken actionId
                let script = Script()
                for arg in args do
                    script.Globals[arg.Key] <- arg.Value;
                return script.DoString(action.Script).ToObject<SynthesizedValue>();
            }
            