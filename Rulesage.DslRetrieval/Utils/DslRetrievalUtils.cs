namespace Rulesage.DslRetrieval.Utils;

internal static class DslRetrievalUtils
{
    internal static float ComputeLevelFactor(float entryLevel, float targetLevel, float levelAlignmentSigma)
    {
        var diff = entryLevel - targetLevel;
        return MathF.Exp(-(diff * diff) / (2 * levelAlignmentSigma * levelAlignmentSigma));
    }

    internal static float ComputeDecayFactor(float averageIdf, float idfPenaltyBeta)
    {
        return 1.0f / (1.0f + idfPenaltyBeta * averageIdf);
    }
}