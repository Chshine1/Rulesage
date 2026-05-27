using Microsoft.FSharp.Collections;
using Rulesage.Common;
using Rulesage.Graph;
using Rulesage.Graph.Services.Abstractions;

namespace Rulesage.Cli.Handlers;

public class NetworkHandler(IGraphBuilder graphBuilder, IGraphDotExporter graphDotExporter, IGraphFuser graphFuser, ILabelPropagator labelPropagator, IModularityService modularityService)
{
    public async Task GenerateDotAsync(string documentPath, string outputPath, CancellationToken cancellationToken = default)
    {
        var document = DocumentParser.Parse(await File.ReadAllTextAsync(documentPath, cancellationToken));
        var graph = await graphBuilder.BuildAsync(document.Rules, document.Records, document.Actions);
        
        var structuralDot = graphDotExporter.ExportDirectional(graph.StructuralLayer);
        var structuralPath = Path.Combine(outputPath, "structural-dot.txt");
        await File.WriteAllTextAsync(structuralPath, structuralDot, cancellationToken);
        
        var semanticDot = graphDotExporter.ExportUndirectional(graph.SemanticLayer);
        var semanticPath = Path.Combine(outputPath, "semantic-dot.txt");
        await File.WriteAllTextAsync(semanticPath, semanticDot, cancellationToken);
    }

    public async Task DiscoverCommunitiesAsync(string documentPath, string outputPath, Dictionary<string, string> ruleLables,
        CancellationToken cancellationToken = default)
    {
        var document = DocumentParser.Parse(await File.ReadAllTextAsync(documentPath, cancellationToken));
        var graph = await graphBuilder.BuildAsync(document.Rules, document.Records, document.Actions);
        
        var fused = graphFuser.Fuse(graph.StructuralLayer, graph.SemanticLayer);
        var seeds = ruleLables.Select(kv => new Tuple<NodeId, string>(NodeId.NewRule(kv.Key), kv.Value));
        var propagated = labelPropagator.Propagate(fused, MapModule.OfSeq(seeds));
        var propagatedDot = graphDotExporter.ExportUndirectionalWithCommunities(fused, propagated);
        var structuralPath = Path.Combine(outputPath, "propagated-dot.txt");
        await File.WriteAllTextAsync(structuralPath, propagatedDot, cancellationToken);

        var modularity = modularityService.Compute(fused, propagated);
        Console.WriteLine($"Modularity: {modularity:F4}");
    }
}