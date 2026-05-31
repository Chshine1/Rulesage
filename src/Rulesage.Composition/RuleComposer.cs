using Rulesage.Common.Grammar.Ast;
using Rulesage.Composition.Services.Abstractions;

namespace Rulesage.Composition;

public class RuleComposer(
    ICompositionContextBuilder contextBuilder,
    IPlanner planner,
    IDslConstrainedDecoder gcd)
    : IRuleComposer
{
    public async Task<RuleExpr> ComposeAsync(
        string subject,
        CancellationToken cancellationToken = default)
    {
        var context = await contextBuilder.BuildAsync("", subject, cancellationToken);
        var plan = await planner.PlanAsync(subject, context, null, cancellationToken);

        return await gcd.DecodeAsync(subject, plan, context, cancellationToken);
    }

    public async Task<RuleExpr> ComposeWithConstrainAsync(string subject, TypeExpr expectedType, string contextCommunity,
        CancellationToken cancellationToken = default)
    {
        var context = await contextBuilder.BuildAsync(contextCommunity, subject, cancellationToken);
        var plan = await planner.PlanAsync(subject, context, expectedType, cancellationToken);

        return await gcd.DecodeAsync(subject, plan, context, cancellationToken);
    }
}