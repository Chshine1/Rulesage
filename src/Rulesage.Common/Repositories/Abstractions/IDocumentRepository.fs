namespace Rulesage.Common.Repositories.Abstractions

open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks

type IDocumentRepository =
    abstract GetDocumentsAsync:
        [<Optional; DefaultParameterValue(CancellationToken())>] cancellationToken: CancellationToken ->
            Task<string seq>
