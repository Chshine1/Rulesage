using Rulesage.Common.Types.Domain;
using Rulesage.Composition.Services.Abstractions;

namespace Rulesage.Composition;

public class OperationComposer(
    ICompositionContextBuilder contextBuilder,
    ISemanticPrecomposer semanticComposer,
    IGrammarGenerator grammarGenerator,
    IDslConstrainedDecoder gcd)
    : IOperationComposer
{
    public async Task<Rule> ComposeAsync(
        string nlTask,
        RuleSignature[] prefetchedOperations,
        CancellationToken cancellationToken = default)
    {
        var context = await contextBuilder.BuildAsync([], [], prefetchedOperations, cancellationToken);
        var semantic = await semanticComposer.ComposeAsync(nlTask, context, cancellationToken);
        var grammar = await grammarGenerator.GenerateAsync(context, cancellationToken);

        return await gcd.DecodeAsync(semantic, context, grammar, cancellationToken);
    }
}