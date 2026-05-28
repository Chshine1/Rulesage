namespace Rulesage.Shared.Services.Abstractions.TextCleaner;

public interface IDocumentSpaceProvider
{
    IDocumentSpace CreateFromMemory(IReadOnlyList<string> documents);
    ValueTask<IDocumentSpace> GetDocumentSpaceFromDbAsync(CancellationToken ct = default);
}