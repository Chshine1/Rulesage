namespace Rulesage.Common.Grammar.Parsers

open FParsec
open Rulesage.Common.Grammar

module Lexer =
    let s = spaces

    let pId: Parser<string> = regex "[a-zA-Z-][a-zA-Z0-9-]*"
    let pKey: Parser<string> = regex "[a-zA-Z][a-zA-Z0-9]*"
    let pDomainTag: Parser<string> = regex "[a-zA-Z]+(\.[a-zA-Z]+)*"

    let pGeneric: Parser<string> =
        regex "[a-zA-Z][a-zA-Z0-9]*" .>> many (skipString "[]")

    let spacedSep (sep: char) (p: Parser<'a>) : Parser<'a list> =
        sepBy p (attempt (s .>> skipChar sep .>> s))

    let spacedSep1 (sep: char) (p: Parser<'a>) : Parser<'a list> =
        sepBy1 p (attempt (s .>> skipChar sep .>> s))

    let genericArgs: Parser<string list> =
        between (skipChar '<') (skipChar '>') (s >>. spacedSep1 ',' pGeneric .>> s)

    let pGenericId: Parser<string * string list> =
        pId .>>. (opt genericArgs |>> Option.defaultValue [])

    let optKeywordBlock (keyword: string) (p: Parser<'a>) : Parser<'a list> =
        opt (skipString keyword >>. spaces1 >>. spacedSep1 ',' p)
        |>> Option.defaultValue []
