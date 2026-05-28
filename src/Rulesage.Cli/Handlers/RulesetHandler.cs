using Rulesage.Common;
using Rulesage.Shared.Repositories.Abstractions;

namespace Rulesage.Cli.Handlers;

public class RulesetHandler(
    IRecordRepository recordRepository,
    IActionRepository actionRepository,
    IRuleRepository ruleRepository)
{
    public async Task SaveAsync(string documentPath, CancellationToken cancellationToken = default)
    {
        var document = DocumentParser.Parse(await File.ReadAllTextAsync(documentPath, cancellationToken));
        
        await recordRepository.SaveAsync(document.Records, cancellationToken);
        await actionRepository.SaveAsync(document.Actions, cancellationToken);
        await ruleRepository.SaveAsync(document.Rules, cancellationToken);
    }
}