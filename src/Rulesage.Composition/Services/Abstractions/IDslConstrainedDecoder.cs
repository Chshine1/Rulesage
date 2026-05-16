using Rulesage.Common.Grammar.Ast;
using Rulesage.Composition.Types;

namespace Rulesage.Composition.Services.Abstractions;

public interface IDslConstrainedDecoder
{
    Task<RuleExpr> DecodeAsync(
        string semanticOperation,
        CompositionContext compositionContext,
        Grammar grammar,
        CancellationToken cancellationToken = default);
}