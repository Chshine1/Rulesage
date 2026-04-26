using System.Text.Json;
using Rulesage.Common.Types;
using Rulesage.DslComposer.Services.Abstractions;

namespace Rulesage.DslComposer;

public class DslComposer(
    ICompositionContextBuilder contextBuilder,
    ISemanticPrecomposer semanticComposer,
    IGrammarGenerator grammarGenerator,
    IDslConstrainedDecoder gcd) : IDslComposer
{
    public async Task<DslEntry> ComposeAsync(string nlTask, DslEntry[] pretchedEntries,
        CancellationToken cancellationToken = default)
    {
        var context = await contextBuilder.BuildAsync(cancellationToken);

        var semantic = await semanticComposer.ComposeAsync(nlTask, context, cancellationToken);

        var grammar = await grammarGenerator.GenerateAsync(context, cancellationToken);

        var structuredPrompt =
            $"Convert this refined composition to exact DslEntry JSON:\n{JsonSerializer.Serialize(semantic)}";

        return await gcd.DecodeAsync(structuredPrompt, grammar, cancellationToken);
    }
}