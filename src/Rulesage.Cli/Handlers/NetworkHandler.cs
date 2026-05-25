using Rulesage.Common;
using Rulesage.Graph;

namespace Rulesage.Cli.Handlers;

public class NetworkHandler(IGraphBuilder graphBuilder)
{
    public async Task GenerateDotAsync(string documentPath, string outputPath, CancellationToken cancellationToken = default)
    {
        var document = DocumentParser.Parse(await File.ReadAllTextAsync(documentPath, cancellationToken));
        var dot = await graphBuilder.ToDotAsync(document.Rules, document.Records, document.Actions);
        await File.WriteAllTextAsync(outputPath, dot, cancellationToken);
    }
}