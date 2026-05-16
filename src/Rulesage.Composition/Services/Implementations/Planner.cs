using Rulesage.Composition.Services.Abstractions;
using Rulesage.Composition.Types;
using Rulesage.Shared.Services.Abstractions;

namespace Rulesage.Composition.Services.Implementations;

public class Planner(ILlmService llm) : IPlanner
{
    private const string SystemPrompt = 
        """
        Transform a requirement into a step-by-step definitional plan using only the provided rules. 
        Rules are declarative: "For <conditions>, this is <what it defines>". 
        Think of the requirement as "What should X be?" and define X by composing rules.

        Output numbered steps. Prefer these patterns:
        - Rule 'id'
        - with param = value
        - $N for a previous step's output
        - "natural language" for parts without a rule
        - The final step must deliver the required definition. 

        A single step suffices if a rule directly answers the requirement.
        """;

    private const string FewShotUser = 
        """
        Requirement: "All handler-related extension methods for IServiceCollection"

        Available rules:
        - all-extension-method-specs: For a target type, this is the array of all its extension method specs.
        - extension-method: For a type to be extended and an extension method spec, this is its full extension method declaration.
        """;

    private const string FewShotAssistant = 
        """
        1. Get the array of all extension method specs, satisfying Rule 'all-extension-method-specs' with targetType = "IServiceCollection".
        2. "Filter the subset from $1 such that the spec is handler-related."
        3. Get the array of all resulting method declarations, each satisfying Rule 'extension-method' with extendedType = "IServiceCollection", spec = element in $2.
        """;
    
    public async Task<string> PlanAsync(
        string nlStructure,
        CompositionContext context,
        CancellationToken cancellationToken = default)
    {
        var rulesArray = string.Join("\n", context.Rules.Select(r => $"- {r.Id}: {r.Annotation}"));
        var nodesArray = string.Join("\n", context.Nodes.Select(n => $"- {n.Id}: {n.Annotation}"));
        var actionsArray = string.Join("\n", context.Actions.Select(a => $"- {a.Id}: {a.Annotation}"));

        var userPrompt =
            $"""
             Requirement: "{nlStructure}"
             
             Available rules:
             {rulesArray}
             
             Available nodes:
             {nodesArray}
             
             Available actions:
             {actionsArray}
             """;

        var messages = new LlmMessage[]
        {
            new() { Role = LlmMessage.MessageRole.System, Content = SystemPrompt },
            new() { Role = LlmMessage.MessageRole.User, Content = FewShotUser },
            new() { Role = LlmMessage.MessageRole.Assistant, Content = FewShotAssistant },
            new() { Role = LlmMessage.MessageRole.User, Content = userPrompt }
        };

        var result = await llm.CompleteAsync(messages, cancellationToken);
        return result.Content;
    }
}