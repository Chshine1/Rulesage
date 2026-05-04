using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Rulesage.Cli.Handlers;

namespace Rulesage.Cli.Commands.Nodes;

public static partial class NodeCommands
{
    public static Command CreateAddCommand(IServiceProvider serviceProvider)
    {
        var cmd = new Command("add", "Add a node")
        {
            new Option<string>("--ir")
            {
                Required = true
            },
            new Option<string>("--desc")
            {
                Required = true
            },
            new Option<string[]>("--param")
            {
                Required = false,
                DefaultValueFactory = _ => [],
                AllowMultipleArgumentsPerToken = true,
                Description = "<key>=<type>:[target]"
            }
        };

        cmd.SetAction(async (result, cancellationToken) =>
        {
            using var scope = serviceProvider.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<NodesHandler>();
            await handler.AddNodeAsync(
                result.GetRequiredValue<string>("--ir"),
                result.GetRequiredValue<string>("--desc"),
                result.GetRequiredValue<string[]>("--param"), cancellationToken);
        });

        return cmd;
    }
}