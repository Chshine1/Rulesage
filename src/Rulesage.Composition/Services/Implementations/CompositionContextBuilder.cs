using Rulesage.Composition.Services.Abstractions;
using Rulesage.Composition.Types;

namespace Rulesage.Composition.Services.Implementations;

public class CompositionContextBuilder : ICompositionContextBuilder
{
    public Task<CompositionContext> BuildAsync(string nlStructure, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CompositionContext
        {
            Communities = [],
            Rules = [],
            Nodes = [],
            Actions = []
        });
    }
}