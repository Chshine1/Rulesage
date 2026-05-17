using Rulesage.Common.Grammar.Ast;
using Rulesage.Composition.Services.Abstractions;

namespace Rulesage.Composition;

public class RuleComposer(
    ICompositionContextBuilder contextBuilder,
    IPlanner planner,
    ITypeAnnotator typeAnnotator,
    IDslConstrainedDecoder gcd)
    : IRuleComposer
{
    public async Task<RuleExpr> ComposeAsync(
        string nlStructure,
        RuleExpr[] prefetchedRules,
        TypeExpr? expectedType = null,
        CancellationToken cancellationToken = default)
    {
        var context = await contextBuilder.BuildAsync(prefetchedRules, [], [], cancellationToken);
        var plan = await planner.PlanAsync(nlStructure, context, cancellationToken);
        var annotatedPlan = await typeAnnotator.AnnotateAsync(nlStructure, plan, cancellationToken);

        return await gcd.DecodeAsync(nlStructure, annotatedPlan, context, cancellationToken);
    }
}