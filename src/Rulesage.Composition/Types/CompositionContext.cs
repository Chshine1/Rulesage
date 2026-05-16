using Rulesage.Common.Grammar;
using Rulesage.Common.Grammar.Ast;

namespace Rulesage.Composition.Types;

public class CompositionContext
{
    public required RuleExpr[] Rules { get; init; }
    public required NodeExpr[] Nodes { get; init; }
    public required ActionExpr[] Actions { get; init; }
}