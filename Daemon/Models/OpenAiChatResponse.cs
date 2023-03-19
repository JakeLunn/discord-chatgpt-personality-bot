using Newtonsoft.Json;

namespace DiscordChatGPT.Daemon.Models;
public class Choice
{
    [JsonProperty("index")]
    public int? Index { get; set; }

    [JsonProperty("message")]
    public Message Message { get; set; } = default!;

    [JsonProperty("finish_reason")]
    public string FinishReason { get; set; } = default!;
}

public class Message
{
    [JsonProperty("role")]
    public string Role { get; set; } = default!;

    [JsonProperty("content")]
    public string Content { get; set; } = default!;
}

public class OpenAiChatResponse
{
    [JsonProperty("id")]
    public string Id { get; set; } = default!;

    [JsonProperty("object")]
    public string Object { get; set; } = default!;

    [JsonProperty("created")]
    public int? Created { get; set; }

    [JsonProperty("choices")]
    public List<Choice> Choices { get; set; } = default!;

    [JsonProperty("usage")]
    public Usage Usage { get; set; } = default!;
}

public class Usage
{
    [JsonProperty("prompt_tokens")]
    public int? PromptTokens { get; set; }

    [JsonProperty("completion_tokens")]
    public int? CompletionTokens { get; set; }

    [JsonProperty("total_tokens")]
    public int? TotalTokens { get; set; }
}

