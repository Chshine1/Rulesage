using Rulesage.Composition.Services.Abstractions;
using Rulesage.Shared.Services.Abstractions;

namespace Rulesage.Composition.Services.Implementations;

public class TypeAnnotator(ILlmService llm): ITypeAnnotator
{
    private const string SystemPrompt =
        """
        Transform a requirement and a step-by-step plan into a type-annotated call chain.
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
        
        3. CALL_CHAIN
           Once you have all necessary types and semantics, output the final type-annotated chain.
           - Format:
             <step>. <action description> :: <output type>
                - <detail: rule name or quoted natural language, with inline parameters>
           - Keep all parameters concrete. Preserve natural-language gaps in double quotes.
           - If a type contradiction is unresolvable, output: ERROR: <explanation>
        
        Be minimal: request only what's necessary.
        """;

    private const string UserFewShotInit =
        """
        Requirement: "All handler-related extension methods for IServiceCollection"
        
        Plan:
        1. Get the array of all extension method specs, satisfying Rule 'all-extension-method-specs' with targetType = "IServiceCollection".
        2. "Filter the subset from $1 such that the spec is handler-related."
        3. Get the array of all resulting method declarations, each satisfying Rule 'extension-method' with extendedType = "IServiceCollection", spec = element in $2.
        """;

    private const string AssistantFewShotSignaturesRequest =
        """
        REQUEST_SIGNATURES
        - rules: all-extension-method-specs, extension-method
        
        ASSUMPTIONS
        - Step 1 produces an array of node instances, each should represent an extension method's spec.
        """;

    // TODO: Complete this
    private const string UserFewShotSignaturesResponse =
        """
        rule all-extension-method-specs: (targetType: literal) -> node extension-method-spec[]
        rule extension-method-from-spec: (extendedType: literal, spec: node extension-method-spec) -> node method
        node extension-method-spec: { name: literal, parameters: ..., attributes: [...] }
        node method: { signature: literal, body: ... }
        """;

    private const string AssistantFewShotDescriptionsRequest =
        """
        REQUEST_DESCRIPTIONS
        - node: extension-method-spec
        
        INSIGHTS
        - The filter in step 2 needs to identify handler-related specs via attributes or name.
        """;

    // TODO
    private const string UserFewShotDescriptionResponse =
        """
        node extension-method-spec: ""
        """;

    private const string AssistantFewShotFinal =
        """
        CALL_CHAIN
        1. GET all extension method specs :: node extension-method-spec[]
           - Rule 'all-extension-method-specs-for' with targetType = "IServiceCollection"
        2. NATURAL LANGUAGE :: node extension-method-spec[]
           - "Filter the subset from $1 such that the spec is handler-related."
        3. FOR EACH spec in $2, GET method declaration :: node method[]
           - Rule 'extension-method-from-spec' with extendedType = "IServiceCollection", spec = element of $2
        """;

    public async Task<string> AnnotateAsync(string nlStructure, string plan, CancellationToken cancellationToken = default)
    {
        var userPrompt =
            $"""
             Requirement: "{nlStructure}"

             Plan:
             {plan}
             """;
        
        var messages = new LlmMessage[]
        {
            new() { Role = LlmMessage.MessageRole.System, Content = SystemPrompt },
            new() { Role = LlmMessage.MessageRole.User, Content = UserFewShotInit },
            new() { Role = LlmMessage.MessageRole.Assistant, Content = AssistantFewShotSignaturesRequest },
            new() { Role = LlmMessage.MessageRole.User, Content = UserFewShotSignaturesResponse },
            new() { Role = LlmMessage.MessageRole.System, Content = AssistantFewShotDescriptionsRequest },
            new() { Role = LlmMessage.MessageRole.User, Content = UserFewShotDescriptionResponse },
            new() { Role = LlmMessage.MessageRole.Assistant, Content = AssistantFewShotFinal },
            new() { Role = LlmMessage.MessageRole.User, Content = userPrompt }
        };

        var result = await llm.CompleteAsync(messages, cancellationToken);
        return result.Content;
    }
}