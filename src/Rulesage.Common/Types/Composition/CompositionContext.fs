namespace Rulesage.Common.Types.Composition

open Rulesage.Common.Types.Domain

type CompositionContext =
    {
        nodes: Map<string, Identifier>
        converters: Map<string, Identifier>
        operations: Map<string, Identifier>
    }
