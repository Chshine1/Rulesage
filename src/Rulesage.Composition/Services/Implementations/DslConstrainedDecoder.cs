using System.Text.Json;
using Rulesage.Common.Types.Composition;
using Rulesage.Common.Types.Domain;
using Rulesage.Composition.Services.Abstractions;
using Rulesage.Composition.Types;
using Rulesage.Shared.Services.Abstractions;

namespace Rulesage.Composition.Services.Implementations;

public class DslConstrainedDecoder(ILlmService llm, JsonSerializerOptions jsonOptions) : IDslConstrainedDecoder
{
    public async Task<OperationBlueprint> DecodeAsync(
        SemanticOperation semanticOperation,
        CompositionContext compositionContext,
        Grammar grammar,
        CancellationToken cancellationToken = default)
    {
        var prompt =$"Convert the following semantic composition into a precise DslCompositionIr JSON using the provided grammar.\n" +
            $"Semantic composition:\n{JsonSerializer.Serialize(semanticOperation, jsonOptions)}";
        
        // TODO: Use GCD here
        var rawJson = await llm.CompleteAsync(prompt, cancellationToken);

        return JsonSerializer.Deserialize<OperationBlueprint>(rawJson, jsonOptions)
               ?? throw new InvalidOperationException("GCD did not return a valid DslCompositionIr.");
    }
}