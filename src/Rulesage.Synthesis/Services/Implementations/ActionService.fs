namespace Rulesage.Synthesis.Services.Implementations

open Rulesage.Synthesis.Services.Abstractions
open MoonSharp.Interpreter

type ActionService() =
    interface IActionService with
        member _.CallAsync cancellationToken action args =
            let script = Script()
            for arg in args do
                script.Globals[arg.Key] <- arg.Value;
            script.DoString(action.Script).ToObject();
            