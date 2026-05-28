using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Rulesage.Cli.Handlers;

namespace Rulesage.Cli.Commands.Ruleset;

public static partial class RulesetCommands
{
    public static Command CreateSearchCommand(IServiceProvider serviceProvider)
    {
        var cmd = new Command("search", "Load into database")
        {
            new Option<string>("--query")
            {
                Required = true
            },
            new Option<int>("--take")
            {
                Required = true
            }
        };


        cmd.SetAction(async (result, cancellationToken) =>
        {
            using var scope = serviceProvider.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<RulesetHandler>();

            await handler.SearchAsync(
                result.GetRequiredValue<string>("--query"),
                result.GetRequiredValue<int>("--take"),
                cancellationToken);
        });

        return cmd;
    }
}