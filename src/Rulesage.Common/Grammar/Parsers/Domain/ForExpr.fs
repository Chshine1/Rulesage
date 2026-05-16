namespace Rulesage.Common.Grammar.Ast

type ForItem = { Key: string; Type: TypeExpr }

type ForExpr = ForItem list


namespace Rulesage.Common.Grammar.Parsers.Domain

open FParsec
open Rulesage.Common.Grammar
open Rulesage.Common.Grammar.Ast
open Rulesage.Common.Grammar.Parsers.Lexer
open Rulesage.Common.Grammar.Parsers.Types

module For =
    let pForExpr: Parser<ForItem, ParseContext> =
        pKey .>> skipString "(" .>>. pTypeExpr .>> skipString ")"
        |>> fun (k, t) -> { Key = k; Type = t }

    let pForBlock: Parser<ForExpr, ParseContext> =
        opt (skipString "for" >>. skipString ":" >>. sepBy1 pForExpr (skipString ","))
        |>> fun ol ->
            match ol with
            | Some l -> l
            | None -> []
