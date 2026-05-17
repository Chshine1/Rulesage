using Rulesage.Common.Grammar.Ast;
using Rulesage.Composition.Services.Abstractions;
using Rulesage.Composition.Types;
using Rulesage.Shared.Services.Abstractions;

namespace Rulesage.Composition.Services.Implementations;

public class DslConstrainedDecoder(ILlmService llm): IDslConstrainedDecoder
{
    private const string SystemPrompt =
        """
        Translate a structure to be strictly defined and an annotated plan into a DSL rule.
        Each step has a key, a natural-language description, and a type annotation (after ::).
        Convert each description into one DSL expression, using these guidelines:

        - Rule call (e.g., "satisfying rule 'X'"): `satisfying X [where p=v, ...]` (where is ignored if no params are passed)
        - Action call (e.g., "using action 'A'"): `result of A [where p=v, ...]`
        - Node construction (e.g., "Construct node 'N'"): `node N with p=v, ...`
        - Sequence over a set (e.g., "For each element in $key"): `seq <inner-expr> p=[iter] val, ...`
          * <inner-expr> is one of the three above (satisfying ... where, result of ... where, node ... with)
          * Mark parameters that should iterate over an array with `iter`; multiple `iter` parameters must have equal-length arrays and will be iterated simultaneously
          * Parameters without `iter` are passed as-is
        - Natural-language gap (a quoted string): `ref(<type>) "<description>"`, inside which you can embed `{val}` for interpolation

        For parameter values:
        - Use string literals `"value"` (also allow interpolations)
        - Use variable references `$given.key` or `$given.key.field.subfield...` (field access is recursive, e.g., `$given.x.method.body`)
        - Use array literals `[v1, v2, ...]` whose elements can be any of the above

        The final step of the plan becomes the `must be` expression.
        If there is only one step, omit the `given:` block and put the expression directly after `must be:`.

        Output format:
        rule <rule-id>
          given:
            <key>: <expression>
            ...
          must be: <expression>
        """;

    private const string UserFewShot =
        """
        Requirement: "A CsFile node containing deduplicated and sorted service registrations for all service interfaces"

        Annotated Plan:
        allServices: Get all service interfaces, satisfying rule 'all-service-interfaces' :: string[]
        registrationLines: Produce a sequence of registration line using action 'format-registration-line' with interfaceName = element in $allServices :: string[]
        sortedLines: "The lines in $registrationLines deduplicated and sorted alphabetically." :: string[]
        csFile: Construct node 'cs-file' with namespace = "MyApp.Services", usings = ["Microsoft.Extensions.DependencyInjection"], lines = $sortedLines :: node cs-file
        """;

    private const string AssistantFewShot =
        """
        rule cs-file-for-all-service-interfaces
          given:
            allServices: satisfying all-service-interfaces
            registrationLines: seq result of format-registration-line where interfaceName = iter $given.allServices
            sortedLines: ref(literal[]) "The lines in {$given.registrationLines} deduplicated and sorted alphabetically."
          must be: node cs-file with namespace = "MyApp.Services", usings = ["Microsoft.Extensions.DependencyInjection"], lines = $given.sortedLines
        """;

    public async Task<RuleExpr> DecodeAsync(
        string nlStructure, 
        string annotatedPlan,
        CompositionContext compositionContext,
        CancellationToken cancellationToken = default)
    {
        var userPrompt =
            $"""
             Target structure: "{nlStructure}"

             Annotated Plan:
             {annotatedPlan}
             """;
        
        var messages = new LlmMessage[]
        {
            new() { Role = LlmMessage.MessageRole.System, Content = SystemPrompt },
            new() { Role = LlmMessage.MessageRole.User, Content = UserFewShot },
            new() { Role = LlmMessage.MessageRole.Assistant, Content = AssistantFewShot },
            new() { Role = LlmMessage.MessageRole.User, Content = userPrompt }
        };

        var result = await llm.CompleteAsync(messages, cancellationToken);
        return result.Content;
    }
}