using Rulesage.Common.Types;

namespace Rulesage.Synthesis;

public interface IAstSynthesizer
{
    Task<Dictionary<string, SynthesizedValue>> SynthesizeAsync(DslEntry entry,
        CancellationToken cancellationToken = default);
}