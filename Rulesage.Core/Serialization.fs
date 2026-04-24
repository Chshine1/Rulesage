module Rulesage.Core.Serialization

open System.Text.Json
open System.Text.Json.Serialization
open Rulesage.Core.Types

let jsonOptions =
    let opts = JsonSerializerOptions()
    opts.Converters.Add(JsonFSharpConverter())
    opts

let serializeEntry (entry: DslEntry) =
    JsonSerializer.Serialize(entry, jsonOptions)

let deserializeEntry (json: string) =
    JsonSerializer.Deserialize<DslEntry>(json, jsonOptions)