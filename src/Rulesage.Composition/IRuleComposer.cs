using Rulesage.Common.Grammar.Ast;

namespace Rulesage.Composition;

public interface IRuleComposer
{
    Task<RuleExpr> ComposeAsync(
        string nlStructure,
        CancellationToken cancellationToken = default);

    Task<RuleExpr> ComposeWithConstrainAsync(string nlStructure, TypeExpr expectedType, string contextCommunity,
        CancellationToken cancellationToken = default);
}