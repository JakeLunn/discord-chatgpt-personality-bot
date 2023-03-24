using Daemon.Options;
using DiscordChatGPT.Daemon.Models;
using DiscordChatGPT.Daemon.Options;
using DiscordChatGPT.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;

namespace DiscordChatGPT.Services;

public class OpenAiAccessor
{
    private OpenAiOptions _options;
    private readonly ILogger<OpenAiAccessor> _logger;
    private readonly HttpClient _httpClient;

    public OpenAiAccessor(IOptionsMonitor<OpenAiOptions> options,
        IOptions<Secrets> secrets,
        ILogger<OpenAiAccessor> logger,
        HttpClient httpClient)
    {
        _options = options.CurrentValue;
        options.OnChange(OnOptionsChange);

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secrets.Value.OpenAiApiKey);
        httpClient.BaseAddress = new Uri(_options.BaseUrl);

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

        var responseContentTask = response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException("Error returned from OpenAI API", null, response.StatusCode);
        }

        var result = JsonConvert.DeserializeObject<OpenAiChatResponse>(await responseContentTask);
        var responseText = result?.Choices[0]?.Message?.Content?.TrimStart('\n');
        if (responseText == null)
        {
            throw new InvalidOperationException("Received no content from Open AI API response");
        }

        return new ChatGPTMessage(ChatGPTRole.assistant, responseText);
    }
}