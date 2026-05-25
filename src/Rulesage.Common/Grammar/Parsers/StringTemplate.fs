namespace Rulesage.Common.Grammar.Ast

type StringPart =
    | Literal of string
    | Interpolation of var: VarExpr

type StringTemplate = StringPart seq

namespace Rulesage.Common.Grammar.Parsers

open FParsec
open Rulesage.Common.Grammar
open Rulesage.Common.Grammar.Ast
open Rulesage.Common.Grammar.Parsers.Vars

module Strings =
    let private s = spaces

    let private pStringPart: Parser<StringPart> =
        let pEscaped =
            choice
                [
                    skipString "\\\"" >>% StringPart.Literal "\""
                    skipString "\\\\" >>% StringPart.Literal "\\"
                    skipString "\\{" >>% StringPart.Literal "{"
                    skipString "\\}" >>% StringPart.Literal "}"
                ]

        let pInterpolation =
            between (skipChar '{') (skipChar '}') (s >>. pVarExpr .>> s)
            |>> StringPart.Interpolation

        let pNormalChar = noneOf "\"\\\n{" |>> fun c -> StringPart.Literal(string c)

        choice [ pEscaped; pInterpolation; pNormalChar ]

    let pSingleLineString: Parser<StringTemplate> =
        sepBy1 (between (skipChar '\"') (skipChar '\"') (many pStringPart)) (skipChar '+')
        |>> Seq.concat

    let pAnnotation: Parser<string> =
        let pSection =
            between
                (skipChar '\"')
                (skipChar '\"')
                (manyChars (
                    choice
                        [
                            noneOf "\"\\\n"
                            skipString "\\\\" >>% '\\'
                            skipString "\\\"" >>% '"'
                            skipString "\\n" >>% '\n'
                        ]
                ))

        skipChar '@' >>. sepBy1 pSection (s >>. skipChar '+' >>. s) |>> String.concat ""
