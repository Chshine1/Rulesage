using Rulesage.Common.Types;

namespace Rulesage.DslComposer;

public interface IDslComposer
{
    Task<DslEntry> ComposeAsync(string nlTask, DslEntry[] pretchedEntries, CancellationToken cancellationToken = default);
}