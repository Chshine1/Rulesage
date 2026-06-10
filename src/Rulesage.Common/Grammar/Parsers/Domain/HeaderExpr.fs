namespace Rulesage.Common.Grammar.Ast

open Rulesage.Common.Grammar

type ParamExpr = { Key: string; Type: TypeExpr }

type UnitHeader =
    {
        Domain: string
        Name: Identifier
        Annotation: string
        TypeParams: string list
        Parameters: Map<string, ParamExpr>
    }

namespace Rulesage.Common.Grammar.Parsers.Domain

open FParsec
open Rulesage.Common.Grammar
open Rulesage.Common.Grammar.Ast
open Rulesage.Common.Grammar.Parsers.Lexer
open Rulesage.Common.Grammar.Parsers.Strings
open Rulesage.Common.Grammar.Parsers.Types

module Unit =
    let private s = spaces
    let private s1 = spaces1

    let private pParamExpr: Parser<ParamExpr> =
        pKey .>> s .>>. between (skipChar '(') (skipChar ')') (s >>. pTypeExpr .>> s)
        |>> fun (k, t) -> { Key = k; Type = t }

    let pHeader (domain: string) (keyword: string) (paramKeyword: string) : Parser<UnitHeader> =
        pAnnotation .>> spaces
        .>>. (skipString keyword >>. s1 >>. pGenericId)
        .>>. (s1 >>. optKeywordBlock paramKeyword pParamExpr)
        |>> fun ((a, gs), pl) ->
            {
                Domain = domain
                Name = fst gs
                Annotation = a
                TypeParams = snd gs
                Parameters = pl |> Seq.map (fun p -> p.Key, p) |> Map.ofSeq
            }
