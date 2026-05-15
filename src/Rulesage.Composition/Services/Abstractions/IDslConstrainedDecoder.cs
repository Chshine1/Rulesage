using Rulesage.Common.Grammar.Ast;
using Rulesage.Common.Types.Composition;
using Rulesage.Composition.Types;

namespace Rulesage.Composition.Services.Abstractions;

public interface IDslConstrainedDecoder
{
    Task<RuleExpr> DecodeAsync(
        SemanticOperation semanticOperation,
        CompositionContext compositionContext,
        Grammar grammar,
        CancellationToken cancellationToken = default);
}