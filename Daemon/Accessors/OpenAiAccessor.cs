using DiscordChatGPT.Daemon.Models;
using DiscordChatGPT.Daemon.Options;
using DiscordChatGPT.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RestSharp;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace DiscordChatGPT.Services;

public class OpenAiAccessor
{
    private OpenAiOptions _options;
    private readonly ILogger<OpenAiAccessor> _logger;
    private readonly HttpClient _httpClient;

    public OpenAiAccessor(IOptionsMonitor<OpenAiOptions> options,
        ILogger<OpenAiAccessor> logger,
        HttpClient httpClient)
    {
        _options = options.CurrentValue;
        options.OnChange(OnOptionsChange);

        _logger = logger;
        _httpClient = httpClient;
    }

    private void OnOptionsChange(OpenAiOptions newOptions)
    {
        if (newOptions.BaseUrl != _options.BaseUrl)
        {
            if (!Uri.TryCreate(newOptions.BaseUrl, new UriCreationOptions(), out var newBaseAddress))
            {
                _logger.LogWarning("New value for BaseUrl failed to be parsed into a valid URI. Old BaseAddress will be used.");
            }
            else
            {
                _logger.LogInformation($"Updating BaseUrl to {newBaseAddress}");
                _httpClient.BaseAddress = newBaseAddress;
            }
        }

        if (newOptions.ApiKey != _options.ApiKey)
        {
            _logger.LogInformation("Updating OpenAI ApiKey to new value [...{ApiKeyTruncated}]",
                newOptions.ApiKey[^5..]);

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", newOptions.ApiKey);
        }

        _options = newOptions;
    }

    public async Task<ChatGPTMessage> PostChatGPT(IList<ChatGPTMessage> messages)
    {
        var data = new
        {
            model = _options.Model,
            messages = messages
        };

        var response = await _httpClient.PostAsync("chat/completions", 
            new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json"));

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException("Error returned from OpenAI API", null, response.StatusCode);
        }

        var result = JsonConvert.DeserializeObject<OpenAiChatResponse>(await response.Content.ReadAsStringAsync());
        var responseText = result?.Choices[0]?.Message?.Content?.TrimStart('\n');
        if (responseText == null)
        {
            throw new InvalidOperationException("Received no content from Open AI API response");
        }

        return new ChatGPTMessage(ChatGPTRole.assistant, responseText);
    }
}