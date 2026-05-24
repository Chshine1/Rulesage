using System.Text.Json;
using Rulesage.Cli.Commands.Nodes;
using Rulesage.Common.Grammar.Ast;
using Rulesage.Shared.Repositories.Abstractions;
using Rulesage.Shared.Services.Abstractions;

namespace Rulesage.Cli.Handlers;

public class NodesHandler(
    INodeRepository nodeRepository,
    IEmbeddingService embeddingService,
    JsonSerializerOptions jsonOptions)
{
    public async Task SearchBySemanticQueryAsync(string query, int skip, int take,
        NodeCommands.NodeFormat format,
        CancellationToken cancellationToken = default)
    {
        var vector = embeddingService.GetEmbedding(query);
        var nodes =
            await nodeRepository.FindOrderByCosineDistanceAsync(vector, skip, take, cancellationToken);
        switch (format)
        {
            case NodeCommands.NodeFormat.Json:
                foreach (var operation in nodes)
                {
                    Console.Write(JsonSerializer.Serialize(operation, jsonOptions));
                    Console.WriteLine();
                }

                break;
            case NodeCommands.NodeFormat.Plain:
                foreach (var operation in nodes)
                {
                    PrintDetailed(operation.Item1);
                    Console.WriteLine();
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }
    }

    private static void PrintDetailed(RecordExpr _)
    {
        throw new NotImplementedException();
    }
}