using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Rulesage.Cli.Handlers;

namespace Rulesage.Cli.Commands.Ruleset;

public static partial class RulesetCommands
{
    public static Command CreateSaveCommand(IServiceProvider serviceProvider)
    {
        var cmd = new Command("load", "Load into database")
        {
            new Option<FileInfo>("--input")
            {
                Required = true
            }
        };


        cmd.SetAction(async (result, cancellationToken) =>
        {
            using var scope = serviceProvider.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<RulesetHandler>();

            await handler.SaveAsync(
                result.GetRequiredValue<FileInfo>("--input").FullName,
                cancellationToken);
        });

        return cmd;
    }
}