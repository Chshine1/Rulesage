using Rulesage.Composition.Types;

namespace Rulesage.Composition.Services.Abstractions;

public interface ICompositionContextBuilder
{
    Task<CompositionContext> BuildAsync(string nlStructure, CancellationToken cancellationToken = default);
}