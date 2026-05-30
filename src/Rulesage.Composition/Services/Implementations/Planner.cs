using Rulesage.Composition.Services.Abstractions;
using Rulesage.Composition.Types;
using Rulesage.Shared.Services.Abstractions;

namespace Rulesage.Composition.Services.Implementations;

public class Planner(ILlmService llm) : IPlanner
{
    private const string SystemPrompt = 
        """
        Construct a step‑by‑step plan to interpret a subject into a record tree, using provided Records, Actions, Rules, or by delegating to a Community.
        
        - Records are structures with named properties.
        - Actions are operations that take parameters and produce results.
        - Rules are standard interpretations: they interpret a subject into part of the record tree.
        - Communities are named interpretation capabilities. You can delegate a sub‑interpretation to them.
        
        Output steps with a semantic key, use patterns like:
        - record <record-id> with param1 = value1, param2 = value2, ...
        - result of <action-id> where ...
        - interpretation of <rule-id> where ...
        - sequential: apply one of the above element-wise, using each param = (value|elements in arrayValue) (multiple arrays are processed lockstep, yielding an array of results)
        - delegate to <community‑id> "subject" (the subject is the noun to be interpreted which allows interpolation)
          If no community fits, use: delegate to none "subject"
        - just a value
        
        where values can be:
        - $key, referring to the result of a previous step
        - "a literal string", allowing {$key} interpolation
        - an array [value1, value2, ...]
        
        Every step is purely declarative interpretation or transformation.
        The final step must deliver the required subject, a single step suffices if it answers directly.
        """;

    private const string FewShotUser = 
        """
        Subject: "A CsFile node containing deduplicated and sorted service registrations for all service interfaces"
        
        Records:
        - cs-file: A C# file node with namespace, usings, lines
        
        Actions:
        - format-line: Formats an interface name into a registration statement
        
        Rules:
        - all-interfaces: Interprets the set of all service interfaces
        """;

    private const string FewShotAssistant = 
        """
        all: apply 'all-interfaces'
        lines: sequence of action 'format-line' each with name = elements in $all
        sortedLines: delegate to none "the lines in {$lines} deduplicated and sorted alphabetically"
        csFile: record 'cs-file' with
          namespace = "MyApp.Services",
          usings = ["Microsoft.Extensions.DependencyInjection"],
          lines = $sortedLines
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
            // new() { Role = LlmMessage.MessageRole.User, Content = FewShotUser },
            // new() { Role = LlmMessage.MessageRole.Assistant, Content = FewShotAssistant },
            new() { Role = LlmMessage.MessageRole.User, Content = userPrompt }
        };

        var result = await llm.CompleteAsync(messages, cancellationToken);
        return result.Content;
    }
}