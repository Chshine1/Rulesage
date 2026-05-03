using Microsoft.FSharp.Collections;
using Rulesage.Common.Types;
using Rulesage.Common.Types.Composition;
using Rulesage.Common.Types.Domain;
using Rulesage.Composition.Services.Abstractions;
using Rulesage.Shared.Services.Abstractions;

namespace Rulesage.Composition.Services.Implementations;

public class CompositionContextBuilder : ICompositionContextBuilder
{
    public Task<CompositionContext> BuildAsync(
        Node[] availableNodes,
        Converter[] availableConverters,
        OperationSignature[] prefetchedOperations,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CompositionContext(
            MapModule.OfSeq(availableNodes.Select(n => new Tuple<string, Identifier>(n.id.ir, n.id))),
            MapModule.OfSeq(availableConverters.Select(c => new Tuple<string, Identifier>(c.id.ir, c.id))),
            MapModule.OfSeq(prefetchedOperations.Select(o => new Tuple<string, Identifier>(o.id.ir, o.id)))
        ));
    }
}