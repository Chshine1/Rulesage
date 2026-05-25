using Rulesage.Common;
using Rulesage.Graph;

namespace Rulesage.Cli.Handlers;

public class NetworkHandler(IGraphBuilder graphBuilder)
{
    public async Task GenerateDotAsync(string documentPath, string outputPath, CancellationToken cancellationToken = default)
    {
        var document = DocumentParser.Parse(await File.ReadAllTextAsync(documentPath, cancellationToken));
        var dot = await graphBuilder.ToDotAsync(document.Rules, document.Records, document.Actions);
        var structuralPath = Path.Combine(outputPath, "structural-dot.txt");
        var semanticPath = Path.Combine(outputPath, "semantic-dot.txt");
        await File.WriteAllTextAsync(structuralPath, dot.Item1, cancellationToken);
        await File.WriteAllTextAsync(semanticPath, dot.Item2, cancellationToken);
    }
}