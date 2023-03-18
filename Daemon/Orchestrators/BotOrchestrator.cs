using Discord;
using Discord.Rest;
using Discord.WebSocket;
using DiscordChatGPT.Exceptions;
using DiscordChatGPT.Factories;
using DiscordChatGPT.Models;
using DiscordChatGPT.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace DiscordChatGPT.Daemon.Orchestrators;

public class BotOrchestrator
{
    private readonly DiscordRestClient _client;
    private readonly EmoteOrchestrator _emoteOrchestrator;
    private readonly DataAccessor _dataService;
    private readonly OpenAiAccessor _openAiAccessor;
    private readonly ILogger<BotOrchestrator> _logger;
    private readonly IMemoryCache _cache;

    public BotOrchestrator(DiscordRestClient client,
        EmoteOrchestrator emoteOrchestrator,
        DataAccessor dataService,
        OpenAiAccessor openAiAccessor,
        ILogger<BotOrchestrator> logger,
        IMemoryCache cache)
    {
        _client = client;
        _emoteOrchestrator = emoteOrchestrator;
        _dataService = dataService;
        _openAiAccessor = openAiAccessor;
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

        if (message.Channel is SocketGuildChannel guildChannel)
        {
            if (!_dataService.IsChannelRegistered(guildChannel.Guild.Id, guildChannel.Id))
            {
                _logger.LogWarning("Guild({Guild} => Channel(#{Channel}) is not registered. Will not respond to unregistered guild channels.",
                    guildChannel.Guild.Name, guildChannel.Name);

                return null;
            }

            var messages = new ChatGPTMessagesFactory(_client)
                .InReplyTo(message)
                .WithPrompt(Constants.StartingPromptText)
                .FromChannel(guildChannel.Guild.Id, guildChannel.Id, 20)
                .WithTailPrompt($"The previous messages were from users on the Discord server. " +
                    $"Write a reply to the most recent message as Alex that fits within the context of the whole conversation. " +
                    $"Strictly follow the rules previously laid out.")
                .Build();

            return await SendToChannelAsync(message.Channel, messages);
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(SocketMessage), $"Message Channel was not castable to {nameof(SocketGuildChannel)} type");
        }
    }

    private async Task<IUserMessage?> SendToChannelAsync(IMessageChannel channel, IList<ChatGPTMessage> messages)
    {
        var (success, responseMessage) = await _openAiAccessor.ChatGpt(messages);

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
