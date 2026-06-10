namespace Rulesage.Synthesis.Services.Abstractions

open System.Threading
open System.Threading.Tasks
open Rulesage.Common.Grammar.Ast

type ISubjectResolver =
    abstract member ResolveAsync: cancellationToken: CancellationToken -> subject: string -> Task<RuleExpr>

    abstract member ResolveWithConstraintAsync:
        cancellationToken: CancellationToken -> expectedType: TypeExpr -> subject: string -> Task<RuleExpr>
