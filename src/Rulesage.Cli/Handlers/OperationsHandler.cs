using System.Text.Json;
using Rulesage.Cli.Utils;
using Rulesage.Common.Types.Domain;
using Rulesage.Shared.Repositories.Abstractions;
using Rulesage.Shared.Services.Abstractions;
using OperationCommands = Rulesage.Cli.Commands.Operations.OperationCommands;

namespace Rulesage.Cli.Handlers;

public class OperationsHandler(
    IEmbeddingService embeddingService,
    IOperationRepository operationRepository,
    JsonSerializerOptions jsonOptions)
{
    public async Task SearchBySemanticQueryAsync(string query, int skip, int take,
        OperationCommands.OperationFormat format,
        CancellationToken cancellationToken = default)
    {
        var vector = embeddingService.GetEmbedding(query);
        var operations =
            await operationRepository.FindOrderByCosineDistanceAsync(vector, skip, take, cancellationToken);
        switch (format)
        {
            case OperationCommands.OperationFormat.Json:
                foreach (var operation in operations)
                {
                    Console.Write(JsonSerializer.Serialize(operation, jsonOptions));
                    Console.WriteLine();
                }
                break;
            case OperationCommands.OperationFormat.Table:
                PrintTable(operations.Select(o => o.Item1));
                break;
            case OperationCommands.OperationFormat.Plain:
                foreach (var operation in operations)
                {
                    PrintDetailed(operation.Item1);
                    Console.WriteLine();
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }
    }


    private static void PrintTable(IEnumerable<OperationSignature> ops)
    {
        var header = $"{"ID",-4} {"IR",-12} {"Description",-25} {"Level",-7} {"Params/Out",-12}";
        ConsoleHelper.WriteLineColored(ConsoleColor.Cyan, header);
        Console.WriteLine(new string('-', header.Length));

        foreach (var line in from op in ops
                 let desc = op.description.Length > 25
                     ? op.description[..22] + "..."
                     : op.description
                 select
                     $"{op.id.id,-4} {op.id.ir,-12} {desc,-25} {op.level,-7:F2} p:{op.parameters.Count,-2} o:{op.outputs.Count,-2}")
        {
            Console.WriteLine(line);
        }
    }

    private static void PrintDetailed(OperationSignature op)
    {
        ConsoleHelper.WriteLineColored(ConsoleColor.Yellow, $"▸ Operation: {op.id.id} | ir='{op.id.ir}'");
        Console.WriteLine($"  Description: {op.description}");
        Console.WriteLine($"  Level: {op.level:F2}");
        Console.WriteLine();

        ConsoleHelper.WriteLineColored(ConsoleColor.Green, $"  Parameters ({op.parameters.Count}):");
        ConsoleFormats.PrintParamsMap(op.parameters);
        Console.WriteLine();

        ConsoleHelper.WriteLineColored(ConsoleColor.Green, $"  Outputs ({op.outputs.Count}):");
        ConsoleFormats.PrintParamsMap(op.outputs);
    }
}