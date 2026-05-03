using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Rulesage.Retrieval.Tests")]
namespace Rulesage.Retrieval.Utils;

internal static class OperationRetrievalUtils
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