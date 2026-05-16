using Rulesage.Composition.Types;

namespace Rulesage.Composition.Services.Abstractions;

public interface ITypeAnnotator
{
    Task<string> AnnotateAsync(
        string nlStructure,
        CompositionContext context,
        CancellationToken cancellationToken = default);
}