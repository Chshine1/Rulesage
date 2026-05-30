using Rulesage.Composition.Types;

namespace Rulesage.Composition.Services.Abstractions;

public interface IPlanner
{
    Task<string> PlanAsync(
        string subject,
        CompositionContext context,
        CancellationToken cancellationToken = default);
}