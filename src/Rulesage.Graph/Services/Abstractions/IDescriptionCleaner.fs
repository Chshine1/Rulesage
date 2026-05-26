namespace Rulesage.Graph.Services.Abstractions

type IDescriptionCleaner =
    abstract Clean: size: int -> descriptions: string seq -> string seq
