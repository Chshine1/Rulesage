using Rulesage.Common.Grammar.Ast;
using Rulesage.Composition.Services.Abstractions;
using Rulesage.Composition.Types;
using Rulesage.Shared.Services.Abstractions;

namespace Rulesage.Composition.Services.Implementations;

public class Planner(ILlmService llm) : IPlanner
{
    private const string SystemPrompt = 
        """
        A "subject" is any text - a noun phrase, verb phrase, question, command, or description, that needs to be interpreted into a structured record tree. You are not executing the subject; you are planning how to interpret it.
        
        Construct a step‑by‑step plan to interpret a subject into a record tree, using only provided Records, Actions, Rules, or by delegating to a Community.
        
        - Records are structures with named properties.
        - Actions are operations that take parameters and produce results.
        - Rules are standard interpretations: they interpret a subject into part of the record tree.
        - Communities are named interpretation capabilities. You can delegate a sub‑interpretation subject to them.
        
        Output steps, each with a camelCase semantic key like `<key>: <step>` using only letters [a-zA-Z], use patterns like:
        - `record <record-id> with <field1> = <value1>, <field2> = <value2>, ...`
        - `result of <action-id> where ...`
        - `interpretation of <rule-id> where ...`
        - sequential result: `seq <one of the above patterns>`, where a param value can be a common `<value>` or `iter <array-typed value>`. It produces an array of the original pattern result, where `iter` params are passed each as their array element (multiple arrays are processed lockstep)
        - `(<type>) delegate to <community‑id> "<subject>"` (allows interpolation). If no community fits, use: `delegate to none "<subject>"`
        - just a value: `<value>`
        
        where values can be:
        - `$key(.field)`, referring to the result of a previous step (and their fields for records). Accessing a "field" of an array will map out the elements' field.
        - `"<a literal string>"`, allowing `{$key}` interpolation and `\",\n,\{,\}` escapes
        - an array `[<value1>, <value2>, ...]`
        
        A <type> must be a type expression, either `literal`, `record <record-id>(<generic-params>)` (generic params must be closed) or their arrays, e.g.
        `literal`, `record Tuple<literal, record TypeSpec>[]`, `record TypeSpec[][]`
        Every step will produce an instance of some type, parameters also expect correct types, you will make sure that types match when passing parameters, and the last step's result type matches the expected type (if given)
        You will be given signatures of available records, actions and rules. Apart from these, you have:
        - If A expects (B, C) produces D, then seq A (b, iter c) expects b:B and c:C[], produces D[]
        - A delegate ensures its return type to be the one specified in the head `(<type>)` part
        - A literal string value is of `literal` type, and interpolated values can be of any type
        
        Every step is purely declarative interpretation or transformation.
        The final step must deliver the required subject, a single step suffices if it answers directly, but it still needs a key.
        
        IMPORTANT: Only record, action, rule or community ids provided by user are available, you CANNOT invent any new id
        """;

    private const string FewShotUser = 
        """
        Subject: "refactor the calculateTotal function to extract the tax logic"
        Expected type: 
        
        Available Records:
        - FunctionSpec:
          A function's signature description, used as the target of refactoring
          { name: literal, params: record Param[], returnType: literal }
        - Param:
          Describes a single function parameter
          { name: literal, type: literal }
        - RefactorCommand:
          A structured command record representing a complete refactoring intent
          { action: literal, target: record FunctionSpec, options: record RefactorOptions }
        - RefactorOptions:
          Options for a refactoring operation, e.g., renaming or extracting logic
          { renameTo: literal, extractTo: literal }
        
        Available Actions:
        - ParseFuncSignature:
          Interprets a natural language function reference into a FunctionSpec
          (signature: literal) -> record FunctionSpec
        - NormalizeOptions:
          Converts a natural language phrase about a refactoring intention into structured options
          (rawOpts: literal) -> record RefactorOptions
        
        Available Rules:
        - DefaultReturnType
          Infers a typical return type for a given language when none is provided
          (lang: literal) -> literal
        
        Available Communities:
        - CodePhraseExtractor
          // Extracts the relevant code identifier (e.g., function name) from a longer description
        """;

    private const string FewShotAssistant = 
        """
        targetSig: (literal) delegate to CodePhraseExtractor "calculateTotal function"
        optsPhrase: (literal) delegate to none "extract the tax logic"
        parsedFunc: result of ParseFuncSignature where signature = $targetSig
        options: result of NormalizeOptions where rawOpts = $optsPhrase
        defaultRet: interpretation of DefaultReturnType where lang = "typescript"
        allFuncs: seq result of ParseFuncSignature where signature = iter [$targetSig]
        command: record RefactorCommand with action = "refactor", target = $parsedFunc, options = $options, inferredReturn = $defaultRet
        """;
    
    public async Task<string> PlanAsync(
        string subject,
        CompositionContext context,
        TypeExpr? expectedType = null,
        CancellationToken cancellationToken = default)
    {
        var parts = new List<string>
        {
            $"Subject: {subject}"
        };

        if (expectedType != null)
        {
            parts.Add($"Expected type: {Common.Grammar.Parsers.Types.formatTypeExpr(expectedType)}");
        }
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