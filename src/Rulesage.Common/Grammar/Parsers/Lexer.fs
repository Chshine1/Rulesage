namespace Rulesage.Common.Grammar.Parsers

open FParsec
open Rulesage.Common.Grammar

module Lexer =
    let pId: Parser<string, ParseContext> = regex "[a-zA-Z-][a-zA-Z0-9-]*"
    let pKey: Parser<string, ParseContext> = regex "[a-zA-Z][a-zA-Z0-9]*"

    let pRecordId: Parser<NodeSignature, ParseContext> =
        fun stream ->
            let reply = pId stream

            match reply.Status with
            | Ok ->
                let id = reply.Result
                let ctx = stream.UserState

                match ctx.nodes.TryFind id with
                | Some signature -> Reply(signature)
                | None -> Reply(Error, expected <| $"predefined record (e.g. %s{id})")
            | _ -> Reply(reply.Status, reply.Error)
