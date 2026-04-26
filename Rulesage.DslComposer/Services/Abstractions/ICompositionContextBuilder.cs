using Rulesage.Common.Types;

namespace Rulesage.DslComposer.Services.Abstractions;

public interface ICompositionContextBuilder
{
    Task<CompositionContext> BuildAsync(CancellationToken cancellationToken = default);
}