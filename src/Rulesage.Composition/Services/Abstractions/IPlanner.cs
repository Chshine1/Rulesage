using Rulesage.Composition.Types;

namespace Rulesage.Composition.Services.Abstractions;

public interface IPlanner
{
    Task<string> PlanAsync(
        string nlStructure,
        CompositionContext context,
        CancellationToken cancellationToken = default);
}