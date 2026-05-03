using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Rulesage.Cli.Handlers;

namespace Rulesage.Cli.Commands.Operations;

public static partial class OperationCommands
{
    public enum OperationFormat
    {
        Json,
        Table,
        Plain
    }

    public static Command CreateSearchCommand(IServiceProvider serviceProvider)
    {
        var cmd = new Command("search", "Search operations by text or semantics")
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
            new Option<OperationFormat>("--format")
            {
                Required = false,
                DefaultValueFactory = _ => OperationFormat.Plain
            }
        };

        cmd.SetAction(async (result, cancellationToken) =>
        {
            using var scope = serviceProvider.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<OperationsHandler>();
            await handler.SearchBySemanticQueryAsync(
                result.GetRequiredValue<string>("--query"),
                result.GetRequiredValue<int>("--offset"),
                result.GetRequiredValue<int>("--limit"),
                result.GetRequiredValue<OperationFormat>("--format"), cancellationToken);
        });

        return cmd;
    }
}