namespace Rulesage.Shared.Services.Abstractions;

public interface IIdfService
{
    Task<string> CleanAsync(string text, CancellationToken cancellationToken = default);
}