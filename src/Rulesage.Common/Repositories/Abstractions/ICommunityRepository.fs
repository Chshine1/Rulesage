namespace Rulesage.Common.Repositories.Abstractions

open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks
open Rulesage.Common.Grammar.Ast

type ICommunityRepository =
    inherit IDocumentRepository

    abstract FindOrderByCosineDistanceAsync:
        contextCommunity: string *
        queryVector: float32[] *
        skip: int *
        take: int *
        [<Optional; DefaultParameterValue(CancellationToken())>] cancellationToken: CancellationToken ->
            Task<(CommunityExpr * float32) seq>

    abstract SaveAsync:
        communities: CommunityExpr seq *
        [<Optional; DefaultParameterValue(CancellationToken())>] cancellationToken: CancellationToken ->
            Task<int>
