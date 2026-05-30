using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Rulesage.Cli.Handlers;

namespace Rulesage.Cli.Commands;

public static class CommonCommands
{
    public static Command CreatePlanCommand(IServiceProvider serviceProvider)
    {
        var cmd = new Command("plan", "Truncate database")
        {
            new Option<string>("--input")
            {
                Required = true,
            }
        };

        cmd.SetAction(async (result, cancellationToken) =>
        {
            using var scope = serviceProvider.CreateScope();

            var handler = scope.ServiceProvider.GetRequiredService<CommonHandler>();
            await handler.PlanAsync(result.GetRequiredValue<string>("--input"), cancellationToken);
        });

        return cmd;
    }
}