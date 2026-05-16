using System.Text.Json.Serialization;

namespace Rulesage.Shared.Services.Abstractions;

public class LlmConfig
{
    public required string Endpoint { get; init; }
    public required string ApiKey { get; init; }
    public required string Model { get; init; }
    public required int TimeoutSeconds { get; init; }
}

public class LlmMessage
{
    public enum MessageRole
    {
        [JsonStringEnumMemberName("system")] System,
        [JsonStringEnumMemberName("user")] User,
        [JsonStringEnumMemberName("assistant")] Assistant
    }
    
    public required MessageRole Role { get; init; }
    public required string Content { get; init; }
}

public class LlmRequest
{
    public required string Model { get; init; }
    public required LlmMessage[] Messages { get; init; }
    public double Temperature { get; init; } = 0.3;
    public int MaxTokens { get; init; } = 2048;
}

public class LlmResponse
{
    public required string Content { get; init; }
    public required string FinishReason { get; init; }
}

public interface ILlmService
{
    Task<LlmResponse> CompleteAsync(IEnumerable<LlmMessage> messages, CancellationToken cancellationToken = default);
}