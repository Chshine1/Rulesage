using Rulesage.Composition.Services.Abstractions;
using Rulesage.Shared.Services.Abstractions;

namespace Rulesage.Composition.Services.Implementations;

public class TypeAnnotator(ILlmService llm): ITypeAnnotator
{
    private const string SystemPrompt =
        """
        Transform a structure to be strictly defined and a step-by-step plan into a type-annotated call chain.
        Work interactively, one section per turn, in this order:
        
        1. REQUEST_SIGNATURES
           Inspect the plan and list only the rules, nodes, or actions whose full type signatures you need.
           - Use the plan's Rule/Node/Action ids.
           - Output exactly:
             REQUEST_SIGNATURES (all are optional, can be ignored if the array is empty)
             - rules: <id1>, <id2>, ...
             - nodes: ...
             - actions: ...
           - Optionally, include an ASSUMPTIONS block recording your inferred type assumptions.
        
        2. REQUEST_DESCRIPTIONS
           After receiving the requested signatures, if some gap in the plan is still ambiguous, ask for semantic clarifications.
           - Output exactly:
             REQUEST_DESCRIPTIONS
             - rules: <id1>, <id2>, ...
             - nodes: ...
             - actions: ...
           - Or, if none needed, output:
             REQUEST_DESCRIPTIONS
             None
           - Optionally, include an INSIGHTS block to record your current understanding of the plan's semantics.
        
        3. ANNOTATED PLAN
            With all necessary types and semantics, output the plan annotated with types and dataflow.
            Follow these patterns:
            - Start a step with `key: description :: output_type`
            - Preserve natural-language parts in double quotes
            - Use rule 'id', node 'id', action 'id' to reference capabilities
            - Iteration can be expressed as `element in $key` in the description where $key is the iterated array
            - If a type contradiction is unresolvable, output ERROR: <explanation>
        
        Be minimal: request only what's necessary.
        """;

    private const string UserFewShotInit =
        """
        Target structure: "A CsFile node containing deduplicated and sorted service registrations for all service interfaces"

        Plan:
        allServices: Get all service interfaces, satisfying rule 'all-service-interfaces'
        registrationLines: Produce a sequence of registration lines, each as the result of action 'format-registration-line' with interfaceName = an element of $allServices
        sortedLines: "The lines in $registrationLines deduplicated and sorted alphabetically."
        csFile: Construct node 'cs-file' with namespace = "MyApp.Services", usings = ["Microsoft.Extensions.DependencyInjection"], lines = $sortedLines
        """;

    private const string AssistantFewShotSignaturesRequest =
        """
        REQUEST_SIGNATURES
        - rules: all-service-interfaces
        - nodes: cs-file
        - actions: format-registration-line

        ASSUMPTIONS
        - all-service-interfaces likely returns an array of interface names (string[]).
        - format-registration-line maps an interface name to a string.
        - cs-file needs to be clarified: its properties namespace, usings, lines.
        """;

    private const string UserFewShotSignaturesResponse =
        """
        rule all-service-interfaces: () -> string[]
        node cs-file: { namespace: string, usings: string[], lines: string[] }
        action format-registration-line: (interfaceName: string) -> string
        """;

    private const string AssistantFewShotDescriptionsRequest =
        """
        REQUEST_DESCRIPTIONS
        - actions: format-registration-line

        INSIGHTS
        - The action's exact output format is ambiguous: does it produce a full registration statement like "services.AddSingleton<...>()"?
        - The plan will use it to generate lines of code, so the exact string shape matters.
        """;

    private const string UserFewShotDescriptionResponse =
        """
        action format-registration-line: "Generates a service registration statement: 'services.AddSingleton<I{interfaceName}, {interfaceName}>();' using the interface's simple name."
        """;

    private const string AssistantFewShotFinal =
        """
        allServices: Get all service interfaces, satisfying rule 'all-service-interfaces' :: string[]
        registrationLines: Produce a sequence of registration line using action 'format-registration-line' with interfaceName = element in $allServices :: string[]
        sortedLines: "The lines in $registrationLines deduplicated and sorted alphabetically." :: string[]
        csFile: Construct node 'cs-file' with namespace = "MyApp.Services", usings = ["Microsoft.Extensions.DependencyInjection"], lines = $sortedLines :: node cs-file
        """;

    public async Task<string> AnnotateAsync(string nlStructure, string plan, CancellationToken cancellationToken = default)
    {
        var userPrompt =
            $"""
             Target structure: "{nlStructure}"

             Plan:
             {plan}
             """;
        
        var messages = new LlmMessage[]
        {
            new() { Role = LlmMessage.MessageRole.System, Content = SystemPrompt },
            new() { Role = LlmMessage.MessageRole.User, Content = UserFewShotInit },
            new() { Role = LlmMessage.MessageRole.Assistant, Content = AssistantFewShotSignaturesRequest },
            new() { Role = LlmMessage.MessageRole.User, Content = UserFewShotSignaturesResponse },
            new() { Role = LlmMessage.MessageRole.Assistant, Content = AssistantFewShotDescriptionsRequest },
            new() { Role = LlmMessage.MessageRole.User, Content = UserFewShotDescriptionResponse },
            new() { Role = LlmMessage.MessageRole.Assistant, Content = AssistantFewShotFinal },
            new() { Role = LlmMessage.MessageRole.User, Content = userPrompt }
        };

        var result = await llm.CompleteAsync(messages, cancellationToken);
        return result.Content;
    }
}