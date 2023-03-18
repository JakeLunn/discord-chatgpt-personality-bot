namespace DiscordChatGPT.Options;

public class GlobalDiscordOptions
{
    public string Token { get; set; } = string.Empty;
    public ulong MasterGuildId { get; set; } = 305165959671316491;
    public int GuildTextChannelContextMessagesLimit { get; set; } = 20;
}
