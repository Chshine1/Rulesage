namespace Rulesage.Shared.Services.Abstractions;

public interface IIdfService
{
    Task<float> ComputeAverageIdfAsync(string text, CancellationToken cancellationToken = default);
}

public interface IOperationIdfService: IIdfService;