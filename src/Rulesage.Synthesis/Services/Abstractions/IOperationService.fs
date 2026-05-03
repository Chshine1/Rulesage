namespace Rulesage.Synthesis.Services.Abstractions

open Rulesage.Common.Types.Domain

type IOperationService =
    abstract member FindOneById: id: int -> OperationBlueprint
