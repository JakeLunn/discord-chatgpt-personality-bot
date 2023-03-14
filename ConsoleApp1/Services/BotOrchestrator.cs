using Discord;
using Discord.Rest;
using DiscordChatGPT.Exceptions;
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

    public async Task RespondToChannelAsync(ulong guildId, ulong channelId)
    {
        var guild = await _client
            .GetGuildAsync(guildId);

        var channel = await guild
            .GetTextChannelAsync(channelId);

        if (guild == null || channel == null)
        {
            throw new ChannelNotFoundException("Channel not found", channelId);
        }

        var messages = (await channel!.GetMessagesAsync(20).FlattenAsync())
            .OrderByDescending(c => c.Timestamp)
            .ToList();

        if (messages == null || !messages.Any())
        {
            _logger.LogInformation("No messages found for Guild {GuildId} in Channel {ChannelId}", guild.Id, channel.Id);
            return;
        }

        var cacheKey = $"LASTMESSAGE--GUILD({guild.Id})--CHANNEL({channel.Id})";
        if (_cache.TryGetValue<ulong>(cacheKey, out var messageId))
        {
            // Remove all messages which came before the most recently responded to message.
            messages = messages
                .Where(m => m.Id > messageId)
                .ToList();

            if (!messages.Any())
            {
                _logger.LogInformation("Number of messages for conversation was {Count} after filtering, skipping until next time.", messages?.Count);
                return;
            }
        }

        var chatGptMessages = new List<ChatGPTMessage>
        {
            new ChatGPTMessage
            {
                Role = "system",
                Content = Constants.StartingPromptText,
                Timestamp = DateTimeOffset.MinValue
            }
        };

        foreach (var message in messages)
        {
            chatGptMessages.Add(new ChatGPTMessage
            {
                Role = "user",
                Content = $"{message.Author.Mention}: {message.CleanContent}",
                Timestamp = message.Timestamp
            });
        }

        chatGptMessages.Add(new ChatGPTMessage
        {
            Role = "system",
            Content = $"The previous {messages.Count} messages were from users on the Discord server. " +
            $"Write a message as Alex that fits within the context of that conversation. " +
            $"Strictly follow the rules previously laid out.",
            Timestamp = DateTimeOffset.Now
        });

        // Re-order messages to be ascending according to timestamp
        chatGptMessages = chatGptMessages
            .OrderBy(c => c.Timestamp)
            .ToList();

        var response = await OpenAiService.ChatGpt(chatGptMessages);

        if (response.success)
        {
            var content = response.responseMessage.Content.TransformForDiscord();
            await channel.SendMessageAsync(content);
            _logger.LogInformation("[SENT] {Guild} => {Channel}: {ResponseMessage}", guild.Name, channel.Name, content);

            _cache.Set<ulong>(cacheKey, messages.First().Id);
        }
        else
        {
            _logger.LogError("Failed to send response due to error from OpenAI: {Error}", response.responseMessage.Content);
        }
    }
}
