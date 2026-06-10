namespace Rulesage.Common.Repositories.Abstractions

open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks
open Rulesage.Common.Grammar.Ast

type IConceptRepository =
    inherit IDocumentRepository

    abstract FindByIdsAsync:
        ids: string seq * [<Optional; DefaultParameterValue(CancellationToken())>] cancellationToken: CancellationToken ->
            Task<ConceptExpr seq>

    abstract FindOrderByCosineDistanceAsync:
        contextCommunity: string *
        queryVector: float32[] *
        skip: int *
        take: int *
        [<Optional; DefaultParameterValue(CancellationToken())>] cancellationToken: CancellationToken ->
            Task<(ConceptExpr * float32) seq>

    abstract SaveAsync:
        records: ConceptExpr seq *
        [<Optional; DefaultParameterValue(CancellationToken())>] cancellationToken: CancellationToken ->
            Task<int>
