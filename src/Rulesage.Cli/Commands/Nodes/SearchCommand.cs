using System.CommandLine;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FSharp.Collections;
using Rulesage.Cli.Handlers;
using Rulesage.Common.Types.Domain;

namespace Rulesage.Cli.Commands.Nodes;

public static partial class NodeCommands
{
    public enum NodeFormat
    {
        Json,
        Plain
    }

    public static Command CreateSearchCommand(IServiceProvider serviceProvider)
    {
        var cmd = new Command("search", "Search nodes by text or semantics")
        {
            new Option<string>("--query")
            {
                Required = true
            },
            new Option<int>("--limit")
            {
                Required = false,
                DefaultValueFactory = _ => 20
            },
            new Option<int>("--offset")
            {
                Required = false,
                DefaultValueFactory = _ => 0
            },
            new Option<NodeFormat>("--format")
            {
                Required = false,
                DefaultValueFactory = _ => NodeFormat.Plain
            }
        };

        cmd.SetAction(async (result, cancellationToken) =>
        {
            using var scope = serviceProvider.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<NodesHandler>();
            await handler.SearchBySemanticQueryAsync(
                result.GetRequiredValue<string>("--query"),
                result.GetRequiredValue<int>("--offset"),
                result.GetRequiredValue<int>("--limit"),
                result.GetRequiredValue<NodeFormat>("--format"), cancellationToken);
        });

        return cmd;
    }
}