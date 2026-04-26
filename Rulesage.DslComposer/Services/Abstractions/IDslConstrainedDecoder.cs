using Rulesage.Common.Types;

namespace Rulesage.DslComposer.Services.Abstractions;

public interface IDslConstrainedDecoder
{
    Task<DslEntry> DecodeAsync(string nlTask, string grammar, CancellationToken cancellationToken = default);
}