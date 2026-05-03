using System.CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Rulesage.Cli.Commands;

public static class InitCommand
{
    public static Command CreateInitCommand(IServiceProvider serviceProvider)
    {
        var cmd = new Command("init", "Init database");

        cmd.SetAction(async (_, cancellationToken) =>
        {
            using var scope = serviceProvider.CreateScope();
            
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var migrations = config.GetValue<string[]>("Migrations");
            if (migrations == null) throw new Exception("Missing Migrations");
            
            var dataSource = scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();
            foreach (var migration in migrations)
            {
                var basePath = AppContext.BaseDirectory;
                var sqlPath = Path.Combine(basePath, migration);
                if (!File.Exists(sqlPath))
                    throw new FileNotFoundException($"Migration script not found: {sqlPath}");

                var sql = await File.ReadAllTextAsync(sqlPath, cancellationToken);

                await using var conn = dataSource.CreateConnection();
                await conn.OpenAsync(cancellationToken);
                await using var sqlCommand = new NpgsqlCommand(sql, conn);
                await sqlCommand.ExecuteNonQueryAsync(cancellationToken);
            }
        });

        return cmd;
    }
}