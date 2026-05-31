using Rulesage.Composition.Services.Abstractions;

namespace Rulesage.Cli.Handlers;

public class CommonHandler(ICompositionContextBuilder contextBuilder, IPlanner planner)
{
    public async Task PlanAsync(string subject, CancellationToken cancellationToken = default)
    {
        var context = await contextBuilder.BuildAsync("", subject, cancellationToken);
        var plan = await planner.PlanAsync(subject, context, null, cancellationToken);
        
        Console.WriteLine(plan);
    }
}