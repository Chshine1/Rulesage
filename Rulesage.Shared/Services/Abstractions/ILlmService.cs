namespace Rulesage.Shared.Services.Abstractions;

public interface ILlmService
{
    Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default);
}