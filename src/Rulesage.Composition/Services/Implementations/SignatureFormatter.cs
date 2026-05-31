using Microsoft.FSharp.Collections;
using Rulesage.Common.Grammar.Ast;
using Rulesage.Composition.Services.Abstractions;

namespace Rulesage.Composition.Services.Implementations;

public class SignatureFormatter : ISignatureFormatter
{
    public async Task<string> FormatTypeExprAsync(TypeExpr expr)
    {
        var array = "";
        for (var i = 0; i < expr.Dimension; i++)
        {
            array += "[]";
        }

        var atomic = await FormatAtomicType(expr.Atomic);
        return $"{atomic}{array}";
    }

    private async Task<string> FormatAtomicType(AtomicType expr)
    {
        switch (expr)
        {
            case AtomicType.Generic n:
                return n.name;
            case AtomicType.Record r:
                const string sep = ", ";
                var types = await Task.WhenAll(r.genericParams.Select(FormatTypeExprAsync));
                var generics = r.genericParams.Length == 0
                    ? ""
                    : $"<{string.Join(sep, types)}>";
                return $"record {r}{generics}";
            default:
                return "literal";
        }
    }

    public async Task<string> FormatRecordSignatureAsync(RecordExpr record)
    {
        var fields = await Task.WhenAll(MapModule.ToSeq(record.Fors).Select(t => Task.Run(async () =>
        {
            var type = await FormatTypeExprAsync(t.Item2.Type);
            return $"{t.Item1}: {type}";
        })));
        var f = string.Join("; ", fields);
        return $"{{{f}}}";
    }

    public Task<string> FormatActionSignatureAsync(ActionExpr action)
    {
        throw new NotImplementedException();
    }

    public Task<string> FormatRuleSignatureAsync(RuleExpr rule)
    {
        throw new NotImplementedException();
    }
}