using Discord;
using Discord.Rest;
using Discord.WebSocket;
using DiscordChatGPT.Exceptions;
using DiscordChatGPT.Models;

namespace DiscordChatGPT.Factories;

public class ChatGPTMessagesBuilder
{
    private readonly DiscordRestClient _restClient;

    private ChatGPTMessage? _startPrompt = null;
    private ChatGPTMessage? _tailPrompt = null;
    private ChatGPTMessage? _messageToReplyTo = null;

    private List<ChatGPTMessage> _messages;

    public ChatGPTMessagesBuilder(DiscordRestClient restClient)
    {
        _messages = new List<ChatGPTMessage>();
        _restClient = restClient;
    }

    public ChatGPTMessagesBuilder WithPrompt(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentNullException(nameof(prompt));
        }

        _startPrompt = new ChatGPTMessage(ChatGPTRole.system, prompt, DateTimeOffset.MinValue);

        return this;
    }

    public ChatGPTMessagesBuilder WithTailPrompt(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentNullException(nameof(prompt));
        }

        _tailPrompt = new ChatGPTMessage(ChatGPTRole.system, prompt);

        return this;
    }

    public ChatGPTMessagesBuilder InReplyTo(IMessage message)
    {
        _messageToReplyTo = new ChatGPTMessage(ChatGPTRole.user, message.CleanContent, message.Timestamp);

        return this;
    }

    public ChatGPTMessagesBuilder FromChannel(ulong guildId, ulong channelId, int limit = 20)
        => FromChannelAsync(guildId, channelId, limit).GetAwaiter().GetResult();

    public ChatGPTMessagesBuilder FromChannel(ISocketMessageChannel channel, int limit = 20)
        => FromChannelAsync(channel, limit).GetAwaiter().GetResult();

    private async Task<ChatGPTMessagesBuilder> FromChannelAsync(ulong guildId, ulong channelId, int limit)
    {
        var guild = await _restClient.GetGuildAsync(guildId);
        if (guild == null)
        {
            throw new ResourceNotFoundException(typeof(RestGuild), guildId);
        }

        var channel = await guild.GetTextChannelAsync(channelId);
        if (channel == null)
        {
            throw new ResourceNotFoundException(typeof(RestTextChannel), channelId);
        }

        return await FromChannelAsync(channel, limit);
    }

    private async Task<ChatGPTMessagesBuilder> FromChannelAsync(IMessageChannel channel, int limit)
    {
        var channelMessages = (await channel.GetMessagesAsync(limit)
            .FlattenAsync())
            .ToList();

        foreach (var message in channelMessages)
        {
            _messages.Add(new ChatGPTMessage(
                    ChatGPTRole.user,
                    message.CleanContent,
                    message.Timestamp));
        }

        return this;
    }

    public IList<ChatGPTMessage> Build()
    {
        var result = new List<ChatGPTMessage>();

        if (_startPrompt != null)
        {
            result.Add(_startPrompt);
        }

        if (_messages.Any())
        {
            result.AddRange(_messages.OrderBy(m => m.Timestamp));
        }

        if (_messageToReplyTo != null && !_messages.Contains(_messageToReplyTo))
        {
            result.Add(_messageToReplyTo);
        }

        if (_tailPrompt != null)
        {
            result.Add(_tailPrompt);
        }

        return result;
    }
}
