namespace Rulesage.Common.Grammar.Ast

type StringPart =
    | Literal of string
    | Interpolation of var: VarExpr

namespace Rulesage.Common.Grammar.Parsers

open FParsec
open Rulesage.Common.Grammar
open Rulesage.Common.Grammar.Ast

module Strings =
    let private pStringPart: Parser<StringPart, ParseContext> =
        let pEscaped =
            choice
                [
                    skipString "\\\"" >>% StringPart.Literal "\""
                    skipString "\\\\" >>% StringPart.Literal "\\"
                    skipString "\\{" >>% StringPart.Literal "{"
                    skipString "\\}" >>% StringPart.Literal "}"
                ]

        let pInterpolation =
            between (skipString "{") (skipString "}") Vars.pVarExpr
            |>> StringPart.Interpolation

        let pNormalChar = noneOf "\"\\\n{" |>> fun c -> StringPart.Literal(string c)

        choice [ pEscaped; pInterpolation; pNormalChar ]

    let pSingleLineString: Parser<StringPart list, ParseContext> =
        between (pstring "\"") (pstring "\"") (many pStringPart)

    let pAnnotation: Parser<string, ParseContext> =
        between
            (skipString "@\"")
            (skipString "\"")
            (manyChars (choice [ noneOf "\"\\\n"; skipString "\\\\" >>% '\\'; skipString "\\\"" >>% '"' ]))
