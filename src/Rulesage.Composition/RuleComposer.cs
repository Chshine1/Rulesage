using Rulesage.Common.Grammar.Ast;
using Rulesage.Composition.Services.Abstractions;

namespace Rulesage.Composition;

public class RuleComposer(
    ICompositionContextBuilder contextBuilder,
    IPlanner semanticComposer,
    IGrammarGenerator grammarGenerator,
    IDslConstrainedDecoder gcd)
    : IRuleComposer
{
    public async Task<RuleExpr> ComposeAsync(
        string nlStructure,
        RuleExpr[] prefetchedOperations,
        TypeExpr? expectedType = null,
        CancellationToken cancellationToken = default)
    {
        var context = await contextBuilder.BuildAsync([], [], prefetchedOperations, cancellationToken);
        var semantic = await semanticComposer.ComposeAsync(nlStructure, context, cancellationToken);
        var grammar = await grammarGenerator.GenerateAsync(context, cancellationToken);

        return await gcd.DecodeAsync(semantic, context, grammar, cancellationToken);
    }
}