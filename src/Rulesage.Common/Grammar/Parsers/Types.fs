namespace Rulesage.Common.Grammar.Ast

type AtomicType =
    | Literal
    | Record of id: string * genericParams: TypeExpr list
    | Generic of name: string

and TypeExpr = { Atomic: AtomicType; Dimension: int }

namespace Rulesage.Common.Grammar.Parsers

open FParsec
open Rulesage.Common.Grammar
open Rulesage.Common.Grammar.Ast
open Rulesage.Common.Grammar.Parsers.Lexer

module Types =
    let private s1 = spaces1

    let private pAtomicType, pAtomicTypeRef =
        createParserForwardedToRef<AtomicType, unit> ()

    let pTypeExpr: Parser<TypeExpr> =
        pAtomicType .>>. many (skipString "[]")
        |>> fun (a, l) -> { Atomic = a; Dimension = l.Length }

    pAtomicTypeRef.Value <-
        choice
            [
                attempt (
                    skipString "literal" .>> notFollowedBy (regex "[a-zA-Z0-9-]")
                    >>% AtomicType.Literal
                )
                attempt (
                    skipString "record" >>. s1 >>. pId
                    .>>. opt (between (skipChar '<') (skipChar '>') (s >>. spacedSep1 ',' pTypeExpr))
                    |>> fun (name, args) -> AtomicType.Record(name, defaultArg args [])
                )
                regex "[a-zA-Z-][a-zA-Z0-9-]*" |>> AtomicType.Generic
            ]
