using Rulesage.Common.Grammar.Ast;

namespace Rulesage.Composition.Types;

public class CompositionContext
{
    public required RuleExpr[] Rules { get; init; }
    public required RecordExpr[] Nodes { get; init; }
    public required ActionExpr[] Actions { get; init; }
}