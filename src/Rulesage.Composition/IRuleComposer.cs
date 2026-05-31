using Rulesage.Common.Grammar.Ast;

namespace Rulesage.Composition;

public interface IRuleComposer
{
    Task<RuleExpr> ComposeAsync(
        string subject,
        CancellationToken cancellationToken = default);

    Task<RuleExpr> ComposeWithConstrainAsync(string subject, TypeExpr expectedType, string contextCommunity,
        CancellationToken cancellationToken = default);
}