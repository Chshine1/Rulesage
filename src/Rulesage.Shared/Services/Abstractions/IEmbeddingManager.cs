namespace Rulesage.Shared.Services.Abstractions;

public interface IEmbeddingManager
{
    Task GenerateEmbeddings(CancellationToken cancellationToken = default);
}