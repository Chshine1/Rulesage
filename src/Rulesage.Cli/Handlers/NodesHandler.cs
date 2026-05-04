using System.Text.Json;
using System.Text.RegularExpressions;
using Rulesage.Cli.Commands.Nodes;
using Rulesage.Cli.Utils;
using Rulesage.Common.Types.Domain;
using Rulesage.Shared.Repositories.Abstractions;
using Rulesage.Shared.Services.Abstractions;

namespace Rulesage.Cli.Handlers;

public partial class NodesHandler(
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

    public async Task AddNodeAsync(string ir, string description, List<string> rawParameters,
        CancellationToken cancellationToken = default)
    {
        var parsedParams = ParseParams(rawParameters).ToArray();
        var parsedIds = new List<int>();
        var parsedIrs = new List<string>();

        foreach (var param in parsedParams)
        {
            if (param.Identifier.HasValue && param.Identifier.Value.Item1 != -1)
                parsedIds.Add(param.Identifier.Value.Item1);
            else if (param.Identifier.HasValue)
                parsedIrs.Add(param.Identifier.Value.Item2);
        }

        var missingIrNodes = (await nodeRepository.FindByIdsAsync(parsedIds, cancellationToken)).ToArray();
        var missingIdNodes = (await nodeRepository.FindByIrsAsync(parsedIrs, cancellationToken)).ToArray();

        var paramsMap = new Dictionary<string, ParamType>();
        foreach (var p in parsedParams)
        {
            if (!p.Identifier.HasValue)
            {
                paramsMap.Add(p.Key, p.IsList ? ParamType.NewArray(ParamType.Leaf) : ParamType.Leaf);
                continue;
            }

            var id = p.Identifier.Value.Item1;
            if (id != -1)
            {
                var nodeType = ParamType.NewNode(new Identifier(id, missingIrNodes.First(n => n.id.id == id).id.ir));
                paramsMap.Add(p.Key, p.IsList ? ParamType.NewArray(nodeType) : nodeType);
            }
            else
            {
                var nodeType = ParamType.NewNode(new Identifier(
                    missingIdNodes.First(n => n.id.ir == p.Identifier.Value.Item2).id.id,
                    p.Identifier.Value.Item2));
                paramsMap.Add(p.Key, p.IsList ? ParamType.NewArray(nodeType) : nodeType);
            }
        }

        await nodeRepository.AddAsync(ir, description, paramsMap, cancellationToken);
    }

    private static readonly Regex paramRegex = ParamRegex();

    private record ParseParamResult(string Key, (int, string)? Identifier, bool IsList);

    private static IEnumerable<ParseParamResult> ParseParams(List<string> raw)
    {
        return raw.Select(s =>
        {
            var match = paramRegex.Match(s);
            if (!match.Success) throw new ArgumentNullException(nameof(raw));

            var key = match.Groups[1].Value;
            var typeKind = match.Groups[2].Value;
            var isList = match.Groups[4].Success;

            if (typeKind.Equals("leaf", StringComparison.OrdinalIgnoreCase))
            {
                return new ParseParamResult(key, null, isList);
            }

            var identStr = match.Groups[3].Value;
            var ident = identStr.All(char.IsDigit)
                ? (int.Parse(identStr), "")
                : (-1, identStr);
            return new ParseParamResult(key, ident, isList);
        });
    }

    private static void PrintDetailed(Node node)
    {
        ConsoleHelper.WriteLineColored(ConsoleColor.Yellow, $"▸ Node: {node.id.id} | ir='{node.id.ir}'");
        Console.WriteLine($"  Description: {node.description}");
        Console.WriteLine();

        ConsoleHelper.WriteLineColored(ConsoleColor.Green, $"  Parameters ({node.parameters.Count}):");
        ConsoleFormats.PrintParamsMap(node.parameters);
        Console.WriteLine();
    }

    [GeneratedRegex(@"^\s*(\S+?)\s*=\s*(leaf|node:([^\[]+))(\[\])?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex ParamRegex();
}