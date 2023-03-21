namespace Daemon.Options;

public class Secrets
{
    public string OpenAiApiKey { get; set; } = string.Empty;
    public string DiscordToken { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;
}