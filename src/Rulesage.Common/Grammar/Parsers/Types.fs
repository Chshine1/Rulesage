namespace Rulesage.Common.Grammar.Ast

type AtomicType =
    | Literal
    | Node of id: string

type TypeExpr = { Atomic: AtomicType; Dimension: int }

namespace Rulesage.Common.Grammar.Parsers

open FParsec
open Rulesage.Common.Grammar
open Rulesage.Common.Grammar.Ast

module Types =
    let private pAtomicType: Parser<AtomicType, ParseContext> =
        choice
            [
                skipString "literal" >>% AtomicType.Literal
                skipString "node" >>. Lexer.pNodeId |>> fun n -> AtomicType.Node n.id
            ]

    let pTypeExpr: Parser<TypeExpr, ParseContext> =
        pAtomicType .>>. many (skipString "[]")
        |>> fun (a, l) -> { Atomic = a; Dimension = l.Length }
