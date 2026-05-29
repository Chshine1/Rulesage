namespace Rulesage.Common.Grammar.Ast

open Rulesage.Common.Grammar

type ParamExpr = { Key: string; Type: TypeExpr }

type GivenExpr = { Key: string; Value: ValueExpr }

type RuleExpr =
    {
        Id: Identifier
        Community: string
        Annotation: string
        Fors: Map<string, ParamExpr>
        Givens: Map<string, GivenExpr>
        MustBe: ValueExpr
    }

type RecordExpr =
    {
        Id: Identifier
        Community: string
        Annotation: string
        GenericParams: string list
        Fors: Map<string, ParamExpr>
    }

type ActionExpr =
    {
        Id: Identifier
        Community: string
        Annotation: string
        GenericParams: string list
        Fors: Map<string, ParamExpr>
        Returns: TypeExpr
        Script: string
    }

type CommunityExpr =
    {
        Sections: string seq
        Annotation: string
    }

namespace Rulesage.Common.Grammar.Parsers.Domain

open FParsec
open Rulesage.Common.Grammar
open Rulesage.Common.Grammar.Ast
open Rulesage.Common.Grammar.Parsers.Domain.Value
open Rulesage.Common.Grammar.Parsers.Lexer
open Rulesage.Common.Grammar.Parsers.Types

module Rule =
    let private s = spaces
    let private s1 = spaces1

    let private pParamExpr: Parser<ParamExpr> =
        pKey .>> s .>>. between (skipChar '(') (skipChar ')') (s >>. pTypeExpr .>> s)
        |>> fun (k, t) -> { Key = k; Type = t }

    let private pParamBlock keyword =
        s1 >>. optKeywordBlock keyword pParamExpr

    let private pGivenExpr: Parser<GivenExpr> =
        pKey .>> s .>> skipChar ':' .>> s .>>. pValueExpr
        |>> fun (k, v) -> { Key = k; Value = v }

    let private pGivenBlock: Parser<GivenExpr list> =
        opt (
            (attempt (s1 >>. skipString "given"))
            >>. s
            >>. skipChar ':'
            >>. s
            >>. (pGivenExpr .>>. many (attempt (s1 >>. pGivenExpr)))
            |>> (fun (n, l) -> n :: l)
        )
        |>> Option.defaultValue []

    let private pMustBeExpr: Parser<ValueExpr> =
        s1 >>. skipString "must be" >>. s >>. skipChar ':' >>. s >>. pValueExpr

    let pRule (community: string) (annotation: string) : Parser<RuleExpr> =
        skipString "rule" .>> s1 >>. pId
        .>>. pParamBlock "for"
        .>>. pGivenBlock
        .>>. pMustBeExpr
        |>> fun (((i, fs), gs), m) ->
            {
                Id = i
                Community = community
                Annotation = annotation
                Fors = fs |> Seq.map (fun f -> f.Key, f) |> Map.ofSeq
                Givens = gs |> Seq.map (fun g -> g.Key, g) |> Map.ofSeq
                MustBe = m
            }

    let pRecord (community: string) (annotation: string) : Parser<RecordExpr> =
        skipString "record" .>> s1 >>. pGenericId .>>. pParamBlock "with"
        |>> fun ((i, gs), fs) ->
            {
                Id = i
                Community = community
                Annotation = annotation
                GenericParams = gs
                Fors = fs |> Seq.map (fun f -> f.Key, f) |> Map.ofSeq
            }

    let private pReturnsExpr: Parser<TypeExpr> =
        s1 >>. skipString "returns" >>. s >>. pTypeExpr

    let pAction (community: string) (annotation: string) : Parser<ActionExpr> =
        skipString "action" .>> s1 >>. pGenericId
        .>>. pParamBlock "on"
        .>>. pReturnsExpr
        |>> fun (((i, gs), fs), r) ->
            {
                Id = i
                Community = community
                Annotation = annotation
                GenericParams = gs
                Fors = fs |> Seq.map (fun f -> f.Key, f) |> Map.ofSeq
                Returns = r
                Script = ""
            }
    
    let pCommunity (community: string) (annotation: string) : Parser<CommunityExpr> =
        skipString "community" .>> s1 >>. pKey
        |>> fun s ->
            {
                Sections = [s] |> Seq.append (community.Split '.')
                Annotation = annotation
            }
