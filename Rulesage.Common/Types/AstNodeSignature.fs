namespace Rulesage.Common.Types

type AstNodeSignatureId = int
type AstParamaterKey = string

type AstNodeSignature = {
    id: AstNodeSignatureId
    // A semantic name for building IR
    name: string
    parameters: (AstParamaterKey * AstNodeSignatureId) list
}