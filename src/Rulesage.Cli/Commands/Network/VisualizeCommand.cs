using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Rulesage.Cli.Handlers;

namespace Rulesage.Cli.Commands.Network;

public static class NetworkCommands
{
    public static Command CreateVisualizeCommand(IServiceProvider serviceProvider)
    {
        var cmd = new Command("visualize", "Search nodes by text or semantics")
        {
            new Option<FileInfo>("--input")
            {
                Required = true
            },
            new Option<FileInfo>("--output")
            {
                Required = true
            }
        };

        cmd.SetAction(async (result, cancellationToken) =>
        {
            using var scope = serviceProvider.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<NetworkHandler>();
            await handler.GenerateDotAsync(
                result.GetRequiredValue<FileInfo>("--input").FullName,
                result.GetRequiredValue<FileInfo>("--output").FullName,
                cancellationToken);
        });

        return cmd;
    }
}