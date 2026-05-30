using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rulesage.Shared.Services.Abstractions;

namespace Rulesage.Shared.Services.Implementations;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class LlmConfig
{
    public required string Endpoint { get; init; }
    public required string ApiKey { get; init; }
    public required string Model { get; init; }
}

public class OpenAiCompatibleService : ILlmService
{
    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };
    
    private readonly HttpClient _httpClient;
    private readonly LlmConfig _config;
    private readonly ILogger<OpenAiCompatibleService> _logger;

    public OpenAiCompatibleService(
        HttpClient httpClient,
        IOptions<LlmConfig> config,
        ILogger<OpenAiCompatibleService> logger)
    {
        _httpClient = httpClient;
        _config = config.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_config.Endpoint);
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _config.ApiKey);
    }

    public async Task<LlmResponse> CompleteAsync(
        IEnumerable<LlmMessage> messages,
        CancellationToken cancellationToken = default)
    {
        var messageArray = messages.ToArray();

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Calling LLM completion. Model={Model}, MessageCount={MessageCount}",
                _config.Model, messageArray.Length);
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var request = new LlmRequest
            {
                Model = _config.Model,
                Messages = messageArray
            };
            
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                var requestJson = JsonSerializer.Serialize(request);
                _logger.LogDebug("LLM request body: {RequestBody}", requestJson);
            }

            HttpResponseMessage httpResponse;
            try
            {
                httpResponse = await _httpClient.PostAsJsonAsync(
                    "", request, jsonOptions, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning(
                        "LLM call was cancelled after {ElapsedMs}ms",
                        stopwatch.ElapsedMilliseconds);
                }

                throw;
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex,
                        "HTTP request failed after {ElapsedMs}ms. Error={ErrorMessage}",
                        stopwatch.ElapsedMilliseconds, ex.Message);
                }

                throw;
            }

            stopwatch.Stop();
            var statusCode = (int)httpResponse.StatusCode;

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "LLM HTTP response received. StatusCode={StatusCode}, ElapsedMs={ElapsedMs}",
                    statusCode, stopwatch.ElapsedMilliseconds);
            }

            if (!httpResponse.IsSuccessStatusCode)
            {
                var errorBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning(
                        "LLM returned non-success status code. StatusCode={StatusCode}, Body={ErrorBody}",
                        statusCode, errorBody);
                }

                httpResponse.EnsureSuccessStatusCode();
            }

            OpenAiResponse? openAiResponse;
            try
            {
                openAiResponse = await httpResponse.Content
                    .ReadFromJsonAsync<OpenAiResponse>(jsonOptions, cancellationToken);
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex,
                        "Failed to deserialize LLM response. StatusCode={StatusCode}, ElapsedMs={ElapsedMs}",
                        statusCode, stopwatch.ElapsedMilliseconds);
                }

                throw new InvalidOperationException("Failed to deserialize LLM response.", ex);
            }

            if (openAiResponse == null)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(
                        "Deserialized LLM response is null. StatusCode={StatusCode}, ElapsedMs={ElapsedMs}",
                        statusCode, stopwatch.ElapsedMilliseconds);
                }

                throw new InvalidOperationException("LLM response content was null.");
            }

            var finishReason = openAiResponse.Choices[0].FinishReason;

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "LLM call succeeded. FinishReason={FinishReason}, ElapsedMs={ElapsedMs}",
                    finishReason, stopwatch.ElapsedMilliseconds);
                
                if (openAiResponse.Usage != null){
                    _logger.LogInformation(
                        "LLM token usage: Prompt={PromptTokens}, Completion={CompletionTokens}, Total={TotalTokens}",
                        openAiResponse.Usage.PromptTokens,
                        openAiResponse.Usage.CompletionTokens,
                        openAiResponse.Usage.TotalTokens);
                }
            }

            return new LlmResponse
            {
                Content = openAiResponse.Choices[0].Message.Content,
                FinishReason = finishReason
            };
        }
        finally
        {
            if (stopwatch.IsRunning)
            {
                stopwatch.Stop();
            }
        }
    }

    [UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.Members)]
    private class OpenAiResponse
    {
        public required List<Choice> Choices { get; init; }
        public UsageInfo? Usage { get; init; }

        [UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.Members)]
        public class Choice
        {
            public required LlmMessage Message { get; init; }
            public string? FinishReason { get; init; }
        }
        
        [UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.Members)]
        public class UsageInfo
        {
            public int PromptTokens { get; init; }
            public int CompletionTokens { get; init; }
            public int TotalTokens { get; init; }
        }
    }
}