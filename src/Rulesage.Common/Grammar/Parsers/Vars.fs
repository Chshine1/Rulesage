namespace Rulesage.Common.Grammar.Ast

type VarSource =
    | For
    | Given

type VarExpr =
    {
        Source: VarSource
        Key: string
        Fields: string list
    }

namespace Rulesage.Common.Grammar.Parsers

open FParsec
open Rulesage.Common.Grammar
open Rulesage.Common.Grammar.Ast
open Rulesage.Common.Grammar.Parsers.Lexer

module Vars =
    let private pVarSource: Parser<VarSource> =
        choice [ skipString "$for" >>% VarSource.For; skipString "$given" >>% VarSource.Given ]

    let private pVarSegment: Parser<string> = skipChar '.' >>. pKey

    let pVarExpr: Parser<VarExpr> =
        pVarSource
        >>= fun source ->
            pVarSegment .>>. many (skipChar '.' >>. pKey)
            |>> fun (k, f) -> { Source = source; Key = k; Fields = f }
