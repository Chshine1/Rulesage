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
    let private pVarSource: Parser<VarSource, ParseContext> =
        choice [ skipString "$for" >>% VarSource.For; skipString "$given" >>% VarSource.Given ]

    let private pVarSegment (source: VarSource) : Parser<string, ParseContext> =
        skipChar '.' >>. pKey
        >>= fun key ->
            fun stream ->
                let keys =
                    match source with
                    | For -> stream.UserState.forItemsKeys
                    | Given -> stream.UserState.givenItemsKeys

                match keys |> Seq.contains key with
                | true -> Reply(key)
                | false -> Reply(Error, expected $"%A{source} variable '%s{key}'")

    let pVarExpr: Parser<VarExpr, ParseContext> =
        pVarSource
        >>= fun source ->
            pVarSegment source .>>. many (skipChar '.' >>. pKey)
            |>> fun (k, f) -> { Source = source; Key = k; Fields = f }
