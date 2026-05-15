using Rulesage.Common.Grammar.Ast;

namespace Rulesage.Composition;

public interface IRuleComposer
{
    Task<RuleExpr> ComposeAsync(
        string nlTask,
        RuleExpr[] prefetchedOperations,
        CancellationToken cancellationToken = default);
}