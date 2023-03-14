namespace DiscordChatGPT.Exceptions;

public class ChannelNotFoundException : Exception
{
    public ulong ChannelId { get; set; }
    public ChannelNotFoundException(string message, ulong channelId) : base(message)
    {
        ChannelId = channelId;
    }
}
