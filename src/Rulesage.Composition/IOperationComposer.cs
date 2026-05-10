using Rulesage.Common.Types.Domain;

namespace Rulesage.Composition;

public interface IOperationComposer
{
    Task<Rule> ComposeAsync(
        string nlTask,
        RuleSignature[] prefetchedOperations,
        CancellationToken cancellationToken = default);
}