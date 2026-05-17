using Rulesage.Composition.Services.Abstractions;
using Rulesage.Composition.Types;
using Rulesage.Shared.Services.Abstractions;

namespace Rulesage.Composition.Services.Implementations;

public class Planner(ILlmService llm) : IPlanner
{
    private const string SystemPrompt = 
        """
        Transform a structure to be strictly defined into a step-by-step plan using the provided rules, nodes, and actions.
        - Rules express definitions: "(For <parameters>,) ... must be ..."
        - Nodes are structures you can construct with named properties
        - Actions are operations that take parameters and produce a result
        Think of the target structure as "What should X be?" and define X by composing rules, nodes and actions.

        Output semantically keyed steps. Prefer these patterns:
        - rule 'id', node 'id', action 'id'
        - with param = value
        - $key for a previous step's output
        - "natural language" for parts without a rule/node/action
        - The final step must deliver the required definition

        A single step suffices if it directly answers the requirement.
        """;

    private const string FewShotUser = 
        """
        Target structure: "A CsFile node containing deduplicated and sorted service registrations for all service interfaces"
        
        Available rules:
        - all-service-interfaces: This is the array of all service interface types
        
        Available nodes:
        - cs-file: A node representing a C# file, with properties: namespace, usings, lines
        
        Available actions:
        - format-registration-line: Takes an interface name and returns a service registration statement as a string
        """;

    private const string FewShotAssistant = 
        """
        allServices: Get all service interfaces, satisfying rule 'all-service-interfaces'
        registrationLines: Produce a sequence of registration lines, each as the result of action 'format-registration-line' with interfaceName = an element of $allServices
        sortedLines: "The lines in $registrationLines deduplicated and sorted alphabetically."
        csFile: Construct node 'cs-file' with namespace = "MyApp.Services", usings = ["Microsoft.Extensions.DependencyInjection"], lines = $sortedLines
        """;
    
    public async Task<string> PlanAsync(
        string nlStructure,
        CompositionContext context,
        CancellationToken cancellationToken = default)
    {
        var parts = new List<string>
        {
            $"Target structure: {nlStructure}"
        };

        if (context.Rules.Length != 0)
        {
            var rulesArray = string.Join("\n", context.Rules.Select(r => $"- {r.Id}: {r.Annotation}"));
            parts.Add($"Available rules:\n{rulesArray}");
        }
        if (context.Nodes.Length != 0)
        {
            var nodesArray = string.Join("\n", context.Nodes.Select(n => $"- {n.Id}: {n.Annotation}"));
            parts.Add($"Available nodes:\n{nodesArray}");
        }
        if (context.Actions.Length != 0)
        {
            var actionsArray = string.Join("\n", context.Actions.Select(a => $"- {a.Id}: {a.Annotation}"));
            parts.Add($"Available actions:\n{actionsArray}");
        }

        var userPrompt = string.Join("\n", parts);

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