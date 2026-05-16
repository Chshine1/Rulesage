using System.Net.Http.Headers;
using System.Net.Http.Json;
using JetBrains.Annotations;
using Microsoft.Extensions.Options;
using Rulesage.Shared.Services.Abstractions;

namespace Rulesage.Shared.Services.Implementations;

public class OpenAiCompatibleService : ILlmService
{
    private readonly HttpClient _httpClient;
    private readonly LlmConfig _config;

    public OpenAiCompatibleService(HttpClient httpClient, IOptions<LlmConfig> config)
    {
        _httpClient = httpClient;
        _config = config.Value;
        _httpClient.BaseAddress = new Uri(_config.Endpoint);
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", _config.ApiKey);
    }

    public async Task<LlmResponse> CompleteAsync(IEnumerable<LlmMessage> messages, CancellationToken cancellationToken = default)
    {
        var request = new LlmRequest
        {
            Model = _config.Model,
            Messages = messages.ToArray()
        };
        var response = await _httpClient.PostAsJsonAsync("", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<OpenAiResponse>(cancellationToken);
        if (content == null) throw new Exception("Failed to get response");
        return new LlmResponse
        {
            Content = content.Choices[0].Message.Content,
            FinishReason = content.Choices[0].FinishReason
        };
    }

    [UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.Members)]
    private class OpenAiResponse
    {
        public required List<Choice> Choices { get; init; }
        
        [UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.Members)]
        public class Choice
        {
            public required LlmMessage Message { get; init; }
            public required string FinishReason { get; init; }
        }
    }
}