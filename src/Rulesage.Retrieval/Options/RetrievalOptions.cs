namespace Rulesage.Retrieval.Options;

public class RetrievalOptions
{
    public int CoarseRecallSize { get; set; } = 50;

    public int FinalTopK { get; set; } = 10;

    public float LevelDecayGamma { get; set; } = 0.8f;

    public float LevelAlignmentSigma { get; set; } = 0.1f;

    public float IdfPenaltyBeta { get; set; } = 0.3f;
}