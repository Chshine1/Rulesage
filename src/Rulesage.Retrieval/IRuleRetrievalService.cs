using Rulesage.Common.Grammar.Ast;

namespace Rulesage.Retrieval;


public interface IRuleRetrievalService
{
    Task<RuleExpr[]> RetrieveAsync(
        string nlTask,
        float? targetLevel = null,
        CancellationToken cancellationToken = default);
}