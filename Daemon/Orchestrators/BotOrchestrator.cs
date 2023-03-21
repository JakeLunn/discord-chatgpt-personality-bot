using Discord;
using Discord.Rest;
using Discord.WebSocket;
using DiscordChatGPT.Daemon.Factories;
using DiscordChatGPT.Daemon.Models;
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
    private readonly DataAccessor _dataAccessor;
    private readonly OpenAiAccessor _openAiAccessor;
    private readonly ILogger<BotOrchestrator> _logger;
    private readonly IMemoryCache _cache;

    public BotOrchestrator(DiscordRestClient client,
        EmoteOrchestrator emoteOrchestrator,
        DataAccessor dataAccessor,
        OpenAiAccessor openAiAccessor,
        ILogger<BotOrchestrator> logger,
        IMemoryCache cache)
    {
        _client = client;
        _emoteOrchestrator = emoteOrchestrator;
        _dataAccessor = dataAccessor;
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

        var prompt = GetPrompt(guild.Id);

        var messages = new ChatGPTMessagesBuilder(_client)
            .WithPrompt(prompt)
            .FromChannel(guildId, channelId, 20)
            .WithTailPrompt($"The previous messages were from users on the Discord server. " +
                $"Write a message as Alex that fits within the context of that conversation. " +
                $"Strictly follow the rules previously laid out.")
            .Build();

        return await SendToChannelAsync(guildId, channel, messages);
    }

    public async Task<IUserMessage?> RespondToMentionAsync(SocketMessage message)
    {
        using var _ = _logger.BeginScope("Respond to {User}", message.Author.Username);

        if (message.Channel is SocketGuildChannel guildChannel)
        {
            if (!_dataAccessor.IsChannelRegistered(guildChannel.Guild.Id, guildChannel.Id))
            {
                _logger.LogWarning("Guild({Guild} => Channel(#{Channel}) is not registered. Will not respond to unregistered guild channels.",
                    guildChannel.Guild.Name, guildChannel.Name);

                return null;
            }

            var prompt = GetPrompt(guildChannel.Guild.Id);

            var messages = new ChatGPTMessagesBuilder(_client)
                .InReplyTo(message)
                .WithPrompt(prompt)
                .FromChannel(guildChannel.Guild.Id, guildChannel.Id, 20)
                .WithTailPrompt($"The previous messages were from users on the Discord server. " +
                    $"Write a reply to the most recent message as Alex that fits within the context of the whole conversation. " +
                    $"Strictly follow the rules previously laid out.")
                .Build();

            return await SendToChannelAsync(guildChannel.Guild.Id, message.Channel, messages);
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(SocketMessage), $"Message Channel was not castable to {nameof(SocketGuildChannel)} type");
        }
    }

    public void ResetPersonaFactsToDefault(ulong guildId)
    {
        _dataAccessor.DeleteAllPersonaFactsForGuild(guildId);
        _dataAccessor.BulkInsertPersonaFacts(Constants.DefaultFacts.Select(f => new GuildPersonaFact
        {
            GuildId = guildId,
            Fact = f
        }).ToList());
    }

    private string GetPrompt(ulong guildId)
    {
        var guildFacts = _dataAccessor.GetPersonaFacts(guildId);

        if (guildFacts.Count == 0)
        {
            throw new ResourceNotFoundException(typeof(GuildPersonaFact), guildId);
        }

        var prompt = new ChatGPTPromptBuilder()
            .WithName(Constants.DefaultName)
            .WithFacts(guildFacts.Select(g => g.Fact).ToList())
            .Build();

        return prompt;
    }

    private async Task<IUserMessage?> SendToChannelAsync(ulong guildId, IMessageChannel channel, IList<ChatGPTMessage> messages)
    {
        try
        {
            var result = await _openAiAccessor.PostChatGPT(messages);

            var content = await _emoteOrchestrator.FormatDiscordMessageAsync(guildId, result.Content);

            _logger.LogInformation("Sending: {Message}", content);
            return await channel.SendMessageAsync(content);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error encountered from Open AI Service: {Error}", e.Message);
            return null;
        }
    }
}
