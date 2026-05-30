using Rulesage.Composition.Types;

namespace Rulesage.Composition.Services.Abstractions;

public interface ICompositionContextBuilder
{
    Task<CompositionContext> BuildAsync(string contextCommunity, string nlStructure, CancellationToken cancellationToken = default);
}