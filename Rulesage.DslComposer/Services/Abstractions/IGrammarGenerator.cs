using Rulesage.Common.Types;

namespace Rulesage.DslComposer.Services.Abstractions;

public interface IGrammarGenerator
{
    Task<string> GenerateAsync(CompositionContext compositionContext, CancellationToken cancellationToken = default);
}