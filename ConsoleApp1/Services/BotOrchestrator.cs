using Discord;
using Discord.Rest;
using Discord.WebSocket;
using DiscordChatGPT.Exceptions;
using DiscordChatGPT.Factories;
using DiscordChatGPT.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace DiscordChatGPT.Services;

public class BotOrchestrator
{
    private readonly DiscordRestClient _client;
    private readonly EmoteOrchestrator _emoteOrchestrator;
    private readonly ILogger<BotOrchestrator> _logger;
    private readonly IMemoryCache _cache;

    public BotOrchestrator(DiscordRestClient client,
        EmoteOrchestrator emoteOrchestrator,
        ILogger<BotOrchestrator> logger,
        IMemoryCache cache)
    {
        _client = client;
        _emoteOrchestrator = emoteOrchestrator;
        _logger = logger;
        _cache = cache;
    }

    public async Task<IUserMessage?> RespondToGuildTextChannelAsync(ulong guildId, ulong channelId)
    {
        var guild = await _client
            .GetGuildAsync(guildId);

        var channel = await guild
            .GetTextChannelAsync(channelId);

        if (guild == null)
        {
            throw new ResourceNotFoundException(typeof(RestGuild), guildId);
        }

        if (channel == null)
        {
            throw new ResourceNotFoundException(typeof(RestTextChannel), channelId);
        }

        using var guildScope = _logger.BeginScope("{Guild}", guild.Name);
        using var channelScope = _logger.BeginScope("#{Channel}", channel.Name);

        var messages = new ChatGPTMessagesFactory(_client)
            .WithPrompt(Constants.StartingPromptText)
            .FromChannel(guildId, channelId, 20)
            .WithTailPrompt($"The previous messages were from users on the Discord server. " +
                $"Write a message as Alex that fits within the context of that conversation. " +
                $"Strictly follow the rules previously laid out.")
            .Build();

        return await SendToChannelAsync(channel, messages);
    }

    public async Task<IUserMessage?> RespondToMentionAsync(SocketMessage message)
    {
        using var _ = _logger.BeginScope("Respond to {User}", message.Author.Username);

        var messages = new ChatGPTMessagesFactory(_client)
            .InReplyTo(message)
            .WithPrompt(Constants.StartingPromptText)
            .FromChannel(message.Channel, 20)
            .WithTailPrompt($"The previous messages were from users on the Discord server. " +
                $"Write a reply to the most recent message as Alex that fits within the context of the whole conversation. " +
                $"Strictly follow the rules previously laid out.")
            .Build();

        return await SendToChannelAsync(message.Channel, messages);
    }

    private async Task<IUserMessage?> SendToChannelAsync(IMessageChannel channel, IList<ChatGPTMessage> messages)
    {
        var (success, responseMessage) = await OpenAiService.ChatGpt(messages);

        if (!success)
        {
            _logger.LogError("Error encountered from Open AI Service: {Error}", responseMessage.Content);
            return null;
        }

        var content = await _emoteOrchestrator.FormatDiscordMessageAsync(responseMessage.Content);

        _logger.LogInformation("Sending: {Message}", content);
        return await channel.SendMessageAsync(content);
    }
}
