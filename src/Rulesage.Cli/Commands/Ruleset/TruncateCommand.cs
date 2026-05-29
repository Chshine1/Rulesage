using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Rulesage.Cli.Commands.Ruleset;

public static partial class RulesetCommands
{
    public static Command CreateTruncateCommand(IServiceProvider serviceProvider)
    {
        var cmd = new Command("truncate", "Truncate database");

        cmd.SetAction(async (_, cancellationToken) =>
        {
            using var scope = serviceProvider.CreateScope();

            var dataSource = scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();

            await using var conn = dataSource.CreateConnection();
            await conn.OpenAsync(cancellationToken);
            await using var sqlCommand =
                new NpgsqlCommand(
                    """
                    truncate table rules restart identity;
                    truncate table actions restart identity;
                    truncate table records restart identity;
                    truncate table communities restart identity;
                    """,
                    conn);
            await sqlCommand.ExecuteNonQueryAsync(cancellationToken);
        });

        return cmd;
    }
}