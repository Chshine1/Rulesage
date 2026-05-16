using Rulesage.Common.Grammar.Ast;
using Rulesage.Composition.Types;

namespace Rulesage.Composition.Services.Abstractions;

public interface ICompositionContextBuilder
{
    public Task<CompositionContext> BuildAsync(
        RuleExpr[] availableRules,
        NodeExpr[] availableNodes,
        ActionExpr[] availableActions,
        CancellationToken cancellationToken = default);
}