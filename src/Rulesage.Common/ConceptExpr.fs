namespace Rulesage.Common.Grammar.Ast

type ConceptExpr = { Header: UnitHeader }

namespace Rulesage.Common.Grammar.Parsers.Domain

open FParsec
open Rulesage.Common.Grammar
open Rulesage.Common.Grammar.Ast
open Rulesage.Common.Grammar.Parsers.Domain.Unit

module Concept =
    let private s = spaces
    let private s1 = spaces1

    let pConcept (domain: string) : Parser<ConceptExpr> =
        pHeader domain "concept" "with" |>> (fun h -> { Header = h })
