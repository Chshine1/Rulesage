using System.Text.Json;
using Rulesage.Cli.Commands.Rules;
using Rulesage.Cli.Utils;
using Rulesage.Common.Grammar.Ast;
using Rulesage.Shared.Repositories.Abstractions;
using Rulesage.Shared.Services.Abstractions;

namespace Rulesage.Cli.Handlers;

public class RulesHandler(
    IEmbeddingService embeddingService,
    IRuleRepository ruleRepository,
    JsonSerializerOptions jsonOptions)
{
    public async Task SearchBySemanticQueryAsync(string query, int skip, int take,
        RuleCommands.RuleFormat format,
        CancellationToken cancellationToken = default)
    {
        var vector = embeddingService.GetEmbedding(query);
        var rules =
            await ruleRepository.FindOrderByCosineDistanceAsync(vector, skip, take, cancellationToken);
        switch (format)
        {
            case RuleCommands.RuleFormat.Json:
                foreach (var operation in rules)
                {
                    Console.Write(JsonSerializer.Serialize(operation, jsonOptions));
                    Console.WriteLine();
                }
                break;
            case RuleCommands.RuleFormat.Table:
                PrintTable(rules.Select(o => o.Item1));
                break;
            case RuleCommands.RuleFormat.Plain:
                foreach (var operation in rules)
                {
                    PrintDetailed(operation.Item1);
                    Console.WriteLine();
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }
    }


    private static void PrintTable(IEnumerable<RuleExpr> rules)
    {
        var header = $"{"ID",-16} {"Annotation",-25} {"Fors/Givens",-12}";
        ConsoleHelper.WriteLineColored(ConsoleColor.Cyan, header);
        Console.WriteLine(new string('-', header.Length));

        foreach (var line in from rule in rules
                 let annotation = rule.Annotation.Length > 25
                     ? rule.Annotation[..22] + "..."
                     : rule.Annotation
                 select
                     $"{rule.Id,-16} {annotation,-25} f:{rule.Fors.Count,-2} g:{rule.Givens.Count,-2}")
        {
            Console.WriteLine(line);
        }
    }

    private static void PrintDetailed(RuleExpr _)
    {
        throw new NotImplementedException();
    }
}