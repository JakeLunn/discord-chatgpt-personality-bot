using Discord;
using Discord.Rest;
using Discord.WebSocket;
using DiscordChatGPT.Exceptions;
using DiscordChatGPT.Factories;
using DiscordChatGPT.Models;
using DiscordChatGPT.Utility;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace DiscordChatGPT.Services;

public class BotOrchestrator
{
    private readonly DiscordRestClient _client;
    private readonly ILogger<BotOrchestrator> _logger;
    private readonly IMemoryCache _cache;

    public BotOrchestrator(DiscordRestClient client,
        ILogger<BotOrchestrator> logger,
        IMemoryCache cache)
    {
        _client = client;
        _logger = logger;
        _cache = cache;
    }

    public async Task<IUserMessage?> RespondToChannelAsync(ulong guildId, ulong channelId)
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
        var messages = new ChatGPTMessagesFactory(_client)
            .WithPrompt(Constants.StartingPromptText)
            .FromChannel(message.Channel, 20)
            .WithTailPrompt($"Reply to the message from {message.Author.Mention}. " +
                $"Remember to strictly follow the rules.")
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

        var content = responseMessage.Content.TransformForDiscord();

        return await channel.SendMessageAsync(content);
    }
}
