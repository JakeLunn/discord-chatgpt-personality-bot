namespace DiscordChatGPT.Models;

public class GuildChannelRegistration
{
    public string Id => $"{GuildId}{ChannelId}";
    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }

    public GuildChannelRegistration(ulong guildId, ulong channelId)
    {
        GuildId = guildId;
        ChannelId = channelId;
    }

    public GuildChannelRegistration() { }
}
