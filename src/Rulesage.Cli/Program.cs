using System.CommandLine;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Rulesage.Cli.Commands;
using Rulesage.Cli.Commands.Network;
using Rulesage.Cli.Extensions;
using Rulesage.Composition.Extensions;
using Rulesage.Graph.Extensions;
using Rulesage.Shared.Extensions;
using RulesetCommands = Rulesage.Cli.Commands.Ruleset.RulesetCommands;

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
                    Path.GetFullPath(vocabRelative, basePath), context.Configuration);
                services.AddGraphModule(context.Configuration);
                services.AddCompositionModule();
            })
            .Build();

        var rootCommand = new RootCommand("Rulesage test");
        rootCommand.Subcommands.Add(CommonCommands.CreatePlanCommand(host.Services));
        
        var rulesetCommand = new Command("ruleset");
        rulesetCommand.Subcommands.Add(RulesetCommands.CreateInitCommand(host.Services));
        rulesetCommand.Subcommands.Add(RulesetCommands.CreateTruncateCommand(host.Services));
        rulesetCommand.Subcommands.Add(RulesetCommands.CreateSaveCommand(host.Services));
        rulesetCommand.Subcommands.Add(RulesetCommands.CreateSearchCommand(host.Services));
        
        var networkCommand = new Command("network");
        networkCommand.Subcommands.Add(NetworkCommands.CreateVisualizeCommand(host.Services));
        networkCommand.Subcommands.Add(NetworkCommands.CreateDiscoverCommand(host.Services));

        rootCommand.Subcommands.Add(rulesetCommand);
        rootCommand.Subcommands.Add(networkCommand);

        return rootCommand.Parse(args).Invoke();
    }
}