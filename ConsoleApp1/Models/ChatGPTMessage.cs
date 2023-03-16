using Newtonsoft.Json;

namespace DiscordChatGPT.Models;

public class ChatGPTMessage
{
    [JsonProperty("role")]
    public string Role { get; set; } = string.Empty;
    [JsonProperty("content")]
    public string Content { get; set; } = string.Empty;
    [JsonIgnore]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    
    public ChatGPTMessage(ChatGPTRole role, string content)
    {
        Role = Enum.GetName(role)!;
        Content = content;
        Timestamp = DateTimeOffset.Now;
    }

    public ChatGPTMessage(ChatGPTRole role, string content, DateTimeOffset timestamp)
    {
        Role = Enum.GetName(role)!;
        Content = content;
        Timestamp = timestamp;
    }

    public ChatGPTMessage()
    {
        
    }
}
