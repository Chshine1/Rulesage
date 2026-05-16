using Rulesage.Common.Grammar.Ast;

namespace Rulesage.Composition;

public interface IRuleComposer
{
    Task<RuleExpr> ComposeAsync(
        string nlStructure,
        RuleExpr[] prefetchedOperations,
        TypeExpr? expectedType = null,
        CancellationToken cancellationToken = default);
}