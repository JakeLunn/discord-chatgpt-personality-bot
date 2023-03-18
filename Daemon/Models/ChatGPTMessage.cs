using Newtonsoft.Json;

namespace DiscordChatGPT.Models;

public class ChatGPTMessage : IEquatable<ChatGPTMessage?>
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

    public ChatGPTMessage() { }

    public override bool Equals(object? obj)
    {
        return Equals(obj as ChatGPTMessage);
    }

    public bool Equals(ChatGPTMessage? other)
    {
        return other is not null &&
               Role == other.Role &&
               Content == other.Content &&
               Timestamp.Equals(other.Timestamp);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Role, Content, Timestamp);
    }

    public static bool operator ==(ChatGPTMessage? left, ChatGPTMessage? right)
    {
        return EqualityComparer<ChatGPTMessage>.Default.Equals(left, right);
    }

    public static bool operator !=(ChatGPTMessage? left, ChatGPTMessage? right)
    {
        return !(left == right);
    }
}
