using Rulesage.Composition.Services.Abstractions;
using Rulesage.Composition.Types;
using Rulesage.Shared.Services.Abstractions;

namespace Rulesage.Composition.Services.Implementations;

public class Planner(ILlmService llm) : IPlanner
{
    private const string SystemPrompt = 
        """
        Construct a step-by-step plan of interpreting a subject into a standardized record tree, using the provided records, actions, rules, or delegating to a community.
        - Records are structures constructable with named properties
        - Actions are operations that take parameters and produce a result
        - Rules are standardized interpretations, they interpret (possibly parameterized subjects), producing standardized record trees
        - Communities are encapsulated rulesets with specific capability ranges of interpretation, which can be delegated an interpretation and produce standardized results
        Think of the target as "What should X be?" and define X by composing records, actions, rules and community delegates.

        Output semantically keyed steps. Prefer these patterns:
        - record 'id', action 'id', rule 'id'
        - with param = value
        - $key for a previous step's result
        - ref 'community-id' "Delegate subject to be interpreted"
        - "plain string (natural language generation allowed)"
        - The final step must deliver the required definition

        A single step suffices if it directly answers the requirement.
        """;

    private const string FewShotUser = 
        """
        Subject: "A CsFile node containing deduplicated and sorted service registrations for all service interfaces"
        
        Records:
        - cs-file: A node representing a C# file, with properties: namespace, usings, lines
        
        Actions:
        - format-registration-line: Takes an interface name and returns a service registration statement as a string
        
        Rules:
        - all-service-interfaces: This is the array of all service interface types
        """;

    private const string FewShotAssistant = 
        """
        allServices: Get all service interfaces, satisfying rule 'all-service-interfaces'
        registrationLines: Produce a sequence of registration lines, each as the result of action 'format-registration-line' with interfaceName = an element of $allServices
        sortedLines: "The lines in $registrationLines deduplicated and sorted alphabetically."
        csFile: Construct node 'cs-file' with namespace = "MyApp.Services", usings = ["Microsoft.Extensions.DependencyInjection"], lines = $sortedLines
        """;
    
    public async Task<string> PlanAsync(
        string subject,
        CompositionContext context,
        CancellationToken cancellationToken = default)
    {
        var parts = new List<string>
        {
            $"Subject: {subject}"
        };

        if (context.Records.Length != 0)
        {
            var nodesArray = string.Join("\n", context.Records.Select(n => $"- {n.Id}: {n.Annotation}"));
            parts.Add($"Records:\n{nodesArray}");
        }
        if (context.Actions.Length != 0)
        {
            var actionsArray = string.Join("\n", context.Actions.Select(a => $"- {a.Id}: {a.Annotation}"));
            parts.Add($"Actions:\n{actionsArray}");
        }
        if (context.Rules.Length != 0)
        {
            var rulesArray = string.Join("\n", context.Rules.Select(r => $"- {r.Id}: {r.Annotation}"));
            parts.Add($"Rules:\n{rulesArray}");
        }

        if (context.Communities.Length != 0)
        {
            var communitiesArray = string.Join("\n",
                context.Communities.Select(r => $"- {string.Concat(r.Sections)}: {r.Annotation}"));
            parts.Add($"Communities:\n{communitiesArray}");
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