using System.Text.Json;
using Rulesage.Common.Types;
using Rulesage.DslComposer.Services.Abstractions;
using Rulesage.Shared.Services.Abstractions;

namespace Rulesage.DslComposer.Services.Implementations;

public class SemanticPrecomposer(ILlmService llm, JsonSerializerOptions options) : ISemanticPrecomposer
{
    private static string BuildPrompt(string nlTask, CompositionContext compositionContext)
    {
        var dslList = compositionContext.availableDsls.Select(d => d.semanticName).Aggregate((a, b) => $"{a}, {b}");
        return
            $$"""
              Given a natural language task, output a JSON structure following this schema:
              {
                  "useDsls": ["dsl names from available list: {{dslList}}"],
                  "context": [{"key": "context name", "value": "natural language description of the AST"}],
                  "produce": [{"key": "production name", "value": "natural language description of the filled AST"}],
                  "subtasks": [{"key": "subtask name", "value": "natural language description of what the subtask does"}]
              }
              Task: {{nlTask}}
              """;
    }

    public async Task<SemanticDslEntry> ComposeAsync(string nlTask, CompositionContext compositionContext,
        CancellationToken cancellationToken = default)
    {
        var prompt = BuildPrompt(nlTask, compositionContext);
        var response = await llm.CompleteAsync(prompt, cancellationToken);
        return JsonSerializer.Deserialize<SemanticDslEntry>(response, options) ?? throw new JsonException();
    }
}