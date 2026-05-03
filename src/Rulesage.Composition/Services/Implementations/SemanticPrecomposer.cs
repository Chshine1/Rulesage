using System.Text.Json;
using Rulesage.Common.Types;
using Rulesage.Common.Types.Composition;
using Rulesage.Composition.Services.Abstractions;
using Rulesage.Shared.Services.Abstractions;

namespace Rulesage.Composition.Services.Implementations;

public class SemanticPrecomposer(ILlmService llm, JsonSerializerOptions jsonOptions) : ISemanticPrecomposer
{
    public async Task<SemanticOperation> ComposeAsync(
        string nlTask,
        CompositionContext context,
        CancellationToken cancellationToken = default)
    {
        var operationList = string.Join(", ", context.operations.Keys);
        var nodeList = string.Join(", ", context.nodes.Keys);

        var prompt =
            $$"""
              Compose a new DSL entry by reusing existing ones.
              Available DSL entries: {{operationList}}
              Available AST node signatures and their parameters: {{nodeList}}

              Task: {{nlTask}}

              Output a JSON object strictly following this schema (but use natural language for descriptions when precise values are not mandated by the schema):
              {
                "useDsls": ["<dsl_semantic_name>"],
                "context": [{"key": "<context_key>", "value": "<description of the required AST>"}],
                "produce": [{"key": "<production_key>", "value": "<description of the filled AST>"}],
                "subtasks": [{"key": "<subtask_key>", "value": "<description of what the subtask does>"}]
              }
              """;

        var rawJson = await llm.CompleteAsync(prompt, cancellationToken);
        return JsonSerializer.Deserialize<SemanticOperation>(rawJson, jsonOptions)
               ?? throw new InvalidOperationException("Failed to parse semantic composition.");
    }
}