namespace Rulesage.Common.Grammar

open FParsec

type Identifier = string

type Type =
    | Literal
    | Node of nodeId: Identifier

type NodeSignature =
    {
        id: Identifier
        parameters: Map<string, Type>
    }

type RuleSignature =
    {
        id: Identifier
        fors: Map<string, Type>
        mustBe: Type
    }

type ActionSignature =
    {
        id: Identifier
        parameters: Map<string, Type>
        returns: Type
    }

type ParseContext =
    {
        nodes: Map<Identifier, NodeSignature>
        rules: Map<Identifier, RuleSignature>
        actions: Map<Identifier, ActionSignature>
        forItemsKeys: string list
        givenItemsKeys: string list
    }

type Parser<'a> = Parser<'a, unit>
