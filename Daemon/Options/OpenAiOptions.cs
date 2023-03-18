namespace DiscordChatGPT.Daemon.Options;

public class OpenAiOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string ChatGptApiUrl { get; set; } = "https://api.openai.com/v1/chat/completions";
    public string DalleApiUrl { get; set; } = "https://api.openai.com/v1/images/generations";
}
