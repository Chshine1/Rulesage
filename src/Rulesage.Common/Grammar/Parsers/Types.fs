namespace Rulesage.Common.Grammar.Ast

type AtomicType =
    | Literal
    | Record of id: string * genericParams: string list
    | Generic of name: string

type TypeExpr = { Atomic: AtomicType; Dimension: int }

namespace Rulesage.Common.Grammar.Parsers

open FParsec
open Rulesage.Common.Grammar
open Rulesage.Common.Grammar.Ast
open Rulesage.Common.Grammar.Parsers.Lexer

module Types =
    let private s1 = spaces1

    let private pAtomicType: Parser<AtomicType> =
        choice
            [
                skipString "literal" >>% AtomicType.Literal
                skipString "record" >>. s1 >>. pGenericId |>> AtomicType.Record
                regex "[a-zA-Z][a-zA-Z0-9]*" |>> AtomicType.Generic
            ]

    let pTypeExpr: Parser<TypeExpr> =
        pAtomicType .>>. many (skipString "[]")
        |>> fun (a, l) -> { Atomic = a; Dimension = l.Length }
