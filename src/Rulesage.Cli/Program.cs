using System.CommandLine;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Rulesage.Cli.Commands;
using Rulesage.Cli.Commands.Nodes;
using Rulesage.Cli.Commands.Operations;
using Rulesage.Cli.Extensions;
using Rulesage.Shared.Extensions;

namespace Rulesage.Cli;

[UsedImplicitly]
public class Program
{
    public static int Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration(config =>
            {
                config.AddJsonFile("appsettings.json", optional: false)
                    .AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                var basePath = AppContext.BaseDirectory;
                var dbConnectionString = context.Configuration.GetConnectionString("Npgsql");
                var onnxRelative = context.Configuration["ML:OnnxModel"];
                var vocabRelative = context.Configuration["ML:Vocab"];
                if (dbConnectionString == null || onnxRelative == null || vocabRelative == null)
                {
                    throw new Exception("Missing configuration section");
                }

                services.AddHandlers();
                services.AddSharedModule(dbConnectionString, Path.GetFullPath(onnxRelative, basePath),
                    Path.GetFullPath(vocabRelative, basePath));
            })
            .Build();

        var rootCommand = new RootCommand("Rulesage test");
        
        var initCommand = InitCommand.CreateInitCommand(host.Services);
        rootCommand.Subcommands.Add(initCommand);

        var operationCommand = new Command("operations");

        operationCommand.Subcommands.Add(OperationCommands.CreateSearchCommand(host.Services));

        var nodeCommand = new Command("nodes");

        nodeCommand.Subcommands.Add(NodeCommands.CreateSearchCommand(host.Services));
        nodeCommand.Subcommands.Add(NodeCommands.CreateAddCommand(host.Services));

        rootCommand.Subcommands.Add(operationCommand);
        rootCommand.Subcommands.Add(nodeCommand);

        return rootCommand.Parse(args).Invoke();
    }
}