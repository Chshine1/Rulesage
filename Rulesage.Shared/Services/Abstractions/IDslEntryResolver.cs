using Rulesage.Common.Types;

namespace Rulesage.Shared.Services.Abstractions;

public interface IDslEntryResolver
{
    Task<DslEntry> ResolveAsync(int id, CancellationToken cancellationToken = default);
}