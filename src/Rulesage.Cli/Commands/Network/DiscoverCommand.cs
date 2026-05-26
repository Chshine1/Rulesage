using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Rulesage.Cli.Handlers;

namespace Rulesage.Cli.Commands.Network;

public static partial class NetworkCommands
{
    public static Command CreateDiscoverCommand(IServiceProvider serviceProvider)
    {
        var cmd = new Command("discover", "Search nodes by text or semantics")
        {
            new Option<FileInfo>("--input")
            {
                Required = true
            },
            new Option<DirectoryInfo>("--output")
            {
                Required = true
            },
            new Option<List<string>>("--label")
            {
                Required = false,
                AllowMultipleArgumentsPerToken = true,
                DefaultValueFactory = _ => []
            }
        };

        cmd.SetAction(async (result, cancellationToken) =>
        {
            using var scope = serviceProvider.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<NetworkHandler>();
            var rawLabels = result.GetRequiredValue<List<string>>("--label");
            var labels = rawLabels.Select(rs =>
            {
                var split = rs.Split(':');
                return (split[0], split[1]);
            }).ToDictionary();
            await handler.DiscoverCommunitiesAsync(
                result.GetRequiredValue<FileInfo>("--input").FullName,
                result.GetRequiredValue<DirectoryInfo>("--output").FullName,
                labels,
                cancellationToken);
        });

        return cmd;
    }
}