namespace Rulesage.Common.Types

type SynthesizedValue =
    | SynLeaf of string
    | SynAst of signatureId: int * parameters: Map<string, SynthesizedValue>
