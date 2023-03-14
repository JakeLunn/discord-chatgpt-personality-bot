using Newtonsoft.Json;

namespace DiscordChatGPT.Models;

public class ChatGPTMessage
{
    [JsonProperty("role")]
    public string Role { get; set; } = string.Empty;
    [JsonProperty("content")]
    public string Content { get; set; } = string.Empty;
    [JsonIgnore]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
