namespace Rulesage.Shared.Repositories.Abstractions;

public interface IDocumentRepository
{
    Task<IEnumerable<string>> GetDocumentsAsync(CancellationToken cancellationToken = default);
}