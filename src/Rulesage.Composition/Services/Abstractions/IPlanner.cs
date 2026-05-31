using Rulesage.Common.Grammar.Ast;
using Rulesage.Composition.Types;

namespace Rulesage.Composition.Services.Abstractions;

public interface IPlanner
{
    Task<string> PlanAsync(
        string subject,
        CompositionContext context,
        TypeExpr? expectedType = null,
        CancellationToken cancellationToken = default);
}