using Rulesage.Common.Grammar.Ast;

namespace Rulesage.Composition.Services.Abstractions;

public interface ISignatureFormatter
{
    Task<string> FormatTypeExprAsync(TypeExpr expr);
    Task<string> FormatRecordSignatureAsync(RecordExpr record);
    Task<string> FormatActionSignatureAsync(ActionExpr action);
    Task<string> FormatRuleSignatureAsync(RuleExpr rule);
}