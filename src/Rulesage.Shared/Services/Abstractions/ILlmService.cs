using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Rulesage.Shared.Services.Abstractions;

public class LlmMessage
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MessageRole
    {
        [JsonStringEnumMemberName("system")] System,
        [JsonStringEnumMemberName("user")] User,

        [JsonStringEnumMemberName("assistant")]
        Assistant
    }

    public required MessageRole Role { get; init; }
    public required string Content { get; init; }
}

[UsedImplicitly(ImplicitUseKindFlags.Access, ImplicitUseTargetFlags.WithMembers)]
public class LlmRequest
{
    public required string Model { get; init; }
    public required LlmMessage[] Messages { get; init; }
    public double Temperature { get; init; } = 0.3;
    public int MaxTokens { get; init; } = 2048;
    public bool EnableThinking { get; init; } = false;
}

public class LlmResponse
{
    public required string Content { get; init; }
    public string? FinishReason { get; init; }
}

public interface ILlmService
{
    Task<LlmResponse> CompleteAsync(IEnumerable<LlmMessage> messages, CancellationToken cancellationToken = default);
}