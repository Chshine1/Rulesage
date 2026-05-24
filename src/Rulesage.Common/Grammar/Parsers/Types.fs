namespace Rulesage.Common.Grammar.Ast

type AtomicType =
    | Literal
    | Record of id: string

type TypeExpr = { Atomic: AtomicType; Dimension: int }

namespace Rulesage.Common.Grammar.Parsers

open FParsec
open Rulesage.Common.Grammar
open Rulesage.Common.Grammar.Ast
open Rulesage.Common.Grammar.Parsers.Lexer

module Types =
    let private s1 = spaces1

    let private pAtomicType: Parser<AtomicType, ParseContext> =
        choice
            [
                skipString "literal" >>% AtomicType.Literal
                skipString "record" >>. s1 >>. pRecordId |>> fun n -> AtomicType.Record n.id
            ]

    let pTypeExpr: Parser<TypeExpr, ParseContext> =
        pAtomicType .>>. many (skipString "[]")
        |>> fun (a, l) -> { Atomic = a; Dimension = l.Length }
