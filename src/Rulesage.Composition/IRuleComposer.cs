using Rulesage.Common.Grammar.Ast;
using Rulesage.Common.Types.Domain;

namespace Rulesage.Composition;

public interface IRuleComposer
{
    Task<Rule> ComposeAsync(
        string nlTask,
        RuleExpr[] prefetchedOperations,
        CancellationToken cancellationToken = default);
}