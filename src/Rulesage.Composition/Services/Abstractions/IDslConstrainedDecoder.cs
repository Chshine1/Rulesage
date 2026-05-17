using Rulesage.Common.Grammar.Ast;
using Rulesage.Composition.Types;

namespace Rulesage.Composition.Services.Abstractions;

public interface IDslConstrainedDecoder
{
    Task<RuleExpr> DecodeAsync(
        string nlStructure, 
        string annotatedPlan,
        CompositionContext compositionContext,
        CancellationToken cancellationToken = default);
}