namespace Rulesage.Core.Types

type AstNodeSignatureId = int
type AstParamaterKey = string

type AstNodeSignature = {
    id: AstNodeSignatureId
    name: string
    parameters: (AstParamaterKey * AstNodeSignatureId) list
}