using Rulesage.Common.Types.Composition;
using Rulesage.Composition.Types;

namespace Rulesage.Composition.Services.Abstractions;

public interface IGrammarGenerator
{
    Task<Grammar> GenerateAsync(
        CompositionContext context,
        CancellationToken cancellationToken = default);
}