using Rulesage.Common.Types.Composition;

namespace Rulesage.Composition.Services.Abstractions;

public interface ISemanticPrecomposer
{
    Task<SemanticOperation> ComposeAsync(
        string nlTask,
        CompositionContext context,
        CancellationToken cancellationToken = default);
}