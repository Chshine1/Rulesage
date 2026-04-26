namespace Rulesage.Common.Types

type DslEntryIr = {
    semanticName: string
    dslId: DslId
    description: string
}

type AstNodeSignatureIr = {
    semanticName: string
    signatureId: AstNodeSignatureId
    parameters: (AstParamaterKey * string) list
}

type CompositionContext = {
    availableDsls: DslEntryIr list
    availableAstSignatures: AstNodeSignatureIr list
}