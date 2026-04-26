using Rulesage.Common.Types;

namespace Rulesage.DslComposer.Services.Abstractions;

public interface ISemanticPrecomposer
{
    Task<SemanticDslEntry> ComposeAsync(string nlTask, CompositionContext compositionContext,
        CancellationToken cancellationToken = default);
}