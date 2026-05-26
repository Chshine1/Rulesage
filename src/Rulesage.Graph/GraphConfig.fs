namespace Rulesage.Graph

[<CLIMutable>]
type GraphConfig =
    {
        R: int
        SimThreshold: float
        TfIdfThreshold: float
        GMin: float
        Alpha: float
        PropergateMaxIter: int
    }
