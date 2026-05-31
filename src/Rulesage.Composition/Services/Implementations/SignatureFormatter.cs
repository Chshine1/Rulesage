using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using Rulesage.Common.Grammar.Ast;
using Rulesage.Composition.Services.Abstractions;
using Rulesage.Shared.Repositories.Abstractions;

namespace Rulesage.Composition.Services.Implementations;

public class SignatureFormatter(IActionRepository actionRepository, IRuleRepository ruleRepository)
    : ISignatureFormatter
{
    private TypeExpr CloseGenerics(TypeExpr expr, Dictionary<string, TypeExpr> genericParams)
    {
        switch (expr.Atomic)
        {
            case AtomicType.Generic g:
                var closed = genericParams[g.name];
                return new TypeExpr(closed.Atomic, expr.Dimension + closed.Dimension);
            case AtomicType.Record r:
                var closedRecord = AtomicType.NewRecord(r.id,
                    ListModule.Map(FuncConvert.FromFunc<TypeExpr, TypeExpr>(p => CloseGenerics(p, genericParams)),
                        r.genericParams));
                return new TypeExpr(closedRecord, expr.Dimension);
            default:
                return expr;
        }
    }

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
        return $"{{ {f} }}";
    }

    public async Task<string> FormatActionSignatureAsync(ActionExpr action)
    {
        var parameters = await Task.WhenAll(MapModule.ToSeq(action.Fors).Select(t => Task.Run(async () =>
        {
            var type = await FormatTypeExprAsync(t.Item2.Type);
            return $"{t.Item1}: {type}";
        })));
        var returns = await FormatTypeExprAsync(action.Returns);
        var p = string.Join(", ", parameters);
        return $"({p}) -> {returns}";
    }

    public async Task<string> FormatRuleSignatureAsync(RuleExpr rule)
    {
        while (true)
        {
            var fors = await Task.WhenAll(MapModule.ToSeq(rule.Fors)
                .Select(t => Task.Run(async () =>
                {
                    var type = await FormatTypeExprAsync(t.Item2.Type);
                    return $"{t.Item1}: {type}";
                })));
            var p = string.Join(", ", fors);
            TypeExpr returnType;
            switch (rule.MustBe)
            {
                case ValueExpr.Primitive primitive:
                    returnType = primitive.expr switch
                    {
                        PrimitiveExpr.StringLiteral => new TypeExpr(AtomicType.Literal, 0),
                        _ => throw new NotImplementedException()
                    };
                    break;
                case ValueExpr.Dynamic dyn:
                    switch (dyn.expr)
                    {
                        case DynamicExpr.Record record:
                            returnType = new TypeExpr(AtomicType.NewRecord(record.record.Item1, record.record.Item2),
                                1);
                            break;
                        case DynamicExpr.ResultOf action:
                            var a = (await actionRepository.FindByIdsAsync([action.action.Item1])).First();
                            var closedGenerics = a.GenericParams.Zip(action.action.Item2).ToDictionary();
                            returnType = CloseGenerics(a.Returns, closedGenerics);
                            break;
                        case DynamicExpr.Satisfying r:
                            var rr = await ruleRepository.FindByIdsAsync([r.ruleId]);
                            rule = rr.First();
                            continue;
                    }

                    break;
                case ValueExpr.Seq seq:
                    switch (seq.expr)
                    {
                        case SeqExpr.Record record:
                            returnType = new TypeExpr(AtomicType.NewRecord(record.record.Item1, record.record.Item2),
                                1);
                            break;
                        case SeqExpr.ResultOf action:
                            var a = (await actionRepository.FindByIdsAsync([action.action.Item1])).First();
                            var closedGenerics = a.GenericParams.Zip(action.action.Item2).ToDictionary();
                            returnType = CloseGenerics(a.Returns, closedGenerics);
                            break;
                        case SeqExpr.Satisfying r:
                            var rr = await ruleRepository.FindByIdsAsync([r.ruleId]);
                            rule = rr.First();
                            continue;
                    }

                    break;
            }

            return $"({p}) -> {FormatTypeExprAsync(returnType)}";
        }
    }
}