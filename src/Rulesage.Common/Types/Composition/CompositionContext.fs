namespace Rulesage.Common.Types.Composition

open Rulesage.Common.Types.Domain

type CompositionContext =
    {
        nodes: string list
        converters: string list
        operations: string list
    }
