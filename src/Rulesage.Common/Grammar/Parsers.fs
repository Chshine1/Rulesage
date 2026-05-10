namespace Rulesage.Common.Grammar

open FParsec

module Parsers =
    let pId: Parser<string, ParseContext> = regex "[a-zA-Z-][a-zA-Z0-9-]*"
    let pKey: Parser<string, ParseContext> = regex "[a-zA-Z][a-zA-Z0-9]*"

    let pNodeId: Parser<NodeSignature, ParseContext> =
        fun stream ->
            let reply = pId stream

            match reply.Status with
            | Ok ->
                let id = reply.Result
                let ctx = stream.UserState

                match ctx.nodes.TryFind id with
                | Some signature -> Reply(signature)
                | None -> Reply(Error, expected <| $"predefined node (e.g. %s{id})")
            | _ -> Reply(reply.Status, reply.Error)

    module Types =
        let private pAtomicType: Parser<AtomicType, ParseContext> =
            choice
                [
                    pstring "literal" >>% AtomicType.Literal
                    pstring "node" >>. pNodeId |>> fun n -> AtomicType.Node n.id
                ]

        let pTypeExpr: Parser<TypeExpr, ParseContext> =
            pAtomicType .>>. many (pstring "[]")
            |>> fun (a, l) -> { Atomic = a; Dimension = l.Length }

    module Vars =
        let private pVarSource: Parser<VarSource, ParseContext> =
            choice [ pstring "$for." >>% VarSource.For; pstring "$given." >>% VarSource.Given ]

        let private pVarSegment (source: VarSource) : Parser<string, ParseContext> =
            pstring "." >>. pKey
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
                let pSeg = pVarSegment source

                pipe2
                    pSeg
                    (many (pstring "." >>. pKey))
                    (fun firstKey restKeys ->
                        {
                            Source = source
                            Key = firstKey
                            Fields = restKeys
                        }
                    )

    module Strings =
        let private pStringPart: Parser<StringPart, ParseContext> =
            let pEscaped =
                choice
                    [
                        pstring "\\\"" >>% StringPart.Literal "\""
                        pstring "\\\\" >>% StringPart.Literal "\\"
                        pstring "\\{" >>% StringPart.Literal "{"
                        pstring "\\}" >>% StringPart.Literal "}"
                    ]

            let pInterpolation =
                between (pstring "{") (pstring "}") Vars.pVarExpr |>> StringPart.Interpolation

            let pNormalChar = noneOf "\"\\\n{" |>> fun c -> StringPart.Literal(string c)

            choice [ pEscaped; pInterpolation; pNormalChar ]

        let pSingleLineString: Parser<StringPart list, ParseContext> =
            between (pstring "\"") (pstring "\"") (many pStringPart)

        let pAnnotation: Parser<string, ParseContext> =
            between
                (pstring "@\"")
                (pstring "\"")
                (manyChars (noneOf "\"\\\n" <|> (pstring "\\\\" >>% '\\') <|> (pstring "\\\"" >>% '"')))

    let pRef: Parser<RefExpr, ParseContext> =
        pstring "ref" >>. between (pstring "(") (pstring ")") Types.pTypeExpr
        .>>. Strings.pSingleLineString
        |>> fun (t, s) -> { ExpctedType = t; Desc = s }

    module Primitives =
        let pPrimitiveExpr, private pPrimitiveExprRef =
            createParserForwardedToRef<PrimitiveExpr, ParseContext> ()

        let private pArrayExpr: Parser<PrimitiveExpr, ParseContext> =
            (pstring "[") >>. sepBy pPrimitiveExpr (pstring ",") .>> (pstring "]")
            |>> PrimitiveExpr.Array

        pPrimitiveExprRef.Value <-
            choice
                [
                    Strings.pSingleLineString |>> PrimitiveExpr.StringLiteral
                    pRef |>> PrimitiveExpr.Ref
                    Vars.pVarExpr |>> PrimitiveExpr.Var
                    pArrayExpr
                ]

    let private pArgExpr: Parser<ArgExpr, ParseContext> =
        pKey .>> pstring "=" .>>. Primitives.pPrimitiveExpr
        |>> fun (k, v) -> { Key = k; Value = v }

    let pArgBlock: Parser<ArgBlock, ParseContext> = sepBy1 pArgExpr (pstring ",")

    let pDynamicExpr: Parser<DynamicExpr, ParseContext> =
        choice
            [
                pstring "satisfying" >>. pId .>>. opt (pstring "where" >>. pArgBlock)
                |>> fun (r, ol) ->
                    DynamicExpr.Satisfying(
                        r,
                        match ol with
                        | Some l -> l
                        | None -> []
                    )
                pstring "result of" >>. pId .>>. opt (pstring "where" >>. pArgBlock)
                |>> fun (a, ol) ->
                    DynamicExpr.ResultOf(
                        a,
                        match ol with
                        | Some l -> l
                        | None -> []
                    )
                pstring "node" >>. pNodeId .>>. opt (pstring "with" >>. pArgBlock)
                |>> fun (n, ol) ->
                    DynamicExpr.Node(
                        n,
                        match ol with
                        | Some l -> l
                        | None -> []
                    )
            ]

    let private pIterArgExpr: Parser<IterArgExpr, ParseContext> =
        pKey .>> pstring "=" .>>. opt (pstring "iter") .>>. Primitives.pPrimitiveExpr
        |>> fun ((k, o), v) -> { Key = k; Value = v; Iter = o.IsSome }

    let pIterArgBlock: Parser<IterArgBlock, ParseContext> =
        sepBy pIterArgExpr (pstring ",")

    let pSeqExpr: Parser<SeqExpr, ParseContext> =
        pstring "seq"
        >>. choice
                [
                    pstring "satisfying" >>. pId .>>. opt (pstring "where" >>. pIterArgBlock)
                    |>> fun (r, ol) ->
                        SeqExpr.Satisfying(
                            r,
                            match ol with
                            | Some l -> l
                            | None -> []
                        )
                    pstring "result of" >>. pId .>>. opt (pstring "where" >>. pIterArgBlock)
                    |>> fun (a, ol) ->
                        SeqExpr.ResultOf(
                            a,
                            match ol with
                            | Some l -> l
                            | None -> []
                        )
                    pstring "node" >>. pNodeId .>>. opt (pstring "with" >>. pIterArgBlock)
                    |>> fun (n, ol) ->
                        SeqExpr.Node(
                            n,
                            match ol with
                            | Some l -> l
                            | None -> []
                        )
                ]

    let pValueExpr: Parser<ValueExpr, ParseContext> =
        choice
            [
                Primitives.pPrimitiveExpr |>> ValueExpr.Primitive
                pDynamicExpr |>> ValueExpr.Dynamic
                pSeqExpr |>> ValueExpr.Seq
            ]
