using System.Text.Json;
using Rulesage.Common.Grammar.Ast;
using Rulesage.Composition.Services.Abstractions;
using Rulesage.Composition.Types;

namespace Rulesage.Composition.Services.Implementations;

public class DslConstrainedDecoder(IPlanner planner, JsonSerializerOptions jsonOptions) : IDslConstrainedDecoder
{
    public async Task<RuleExpr> DecodeAsync(
        string semanticOperation,
        CompositionContext compositionContext,
        Grammar grammar,
        CancellationToken cancellationToken = default)
    {
        var plan = await planner.PlanAsync(semanticOperation, compositionContext, cancellationToken);

        return JsonSerializer.Deserialize<RuleExpr>(plan, jsonOptions)
               ?? throw new InvalidOperationException("GCD did not return a valid DslCompositionIr.");
    }
}