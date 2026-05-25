namespace Rulesage.Common.Grammar.Parsers

open FParsec
open Rulesage.Common.Grammar

module Lexer =
    let pId: Parser<string> = regex "[a-zA-Z-][a-zA-Z0-9-]*"
    let pKey: Parser<string> = regex "[a-zA-Z][a-zA-Z0-9]*"
