using Discord;
using Discord.Rest;
using Discord.WebSocket;
using DiscordChatGPT.Exceptions;
using DiscordChatGPT.Models;
using DiscordChatGPT.Options;
using DiscordChatGPT.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace DiscordChatGPT;

public class TimedHostedService : IHostedService, IDisposable
{
    private int executionCount = 0;
    private readonly ILogger<TimedHostedService> _logger;
    private readonly DiscordRestClient _client;
    private readonly IMemoryCache _cache;
    private readonly DataService _db;
    private readonly IOptions<TimedHostOptions> _options;
    private readonly static Random _random = new();
    private Timer? _timer = null;

    public TimedHostedService(ILogger<TimedHostedService> logger,
        DiscordRestClient client,
        IMemoryCache cache,
        DataService db,
        IOptions<TimedHostOptions> options)
    {
        _logger = logger;
        _client = client;
        _cache = cache;
        _db = db;
        _options = options;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("{Service} has started", nameof(TimedHostedService));

        _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.Parse(_options.Value.TimedHostTimeSpan));

        return Task.CompletedTask;
    }

    private void DoWork(object? state)
        => DoWorkAsync(state).GetAwaiter().GetResult();

    private async Task DoWorkAsync(object? state)
    {
        var count = Interlocked.Increment(ref executionCount);

        _logger.LogInformation(
            "{Service} is working. Count: {Count}", nameof(TimedHostedService), executionCount);

        var roll = _random.Next(10);
        _logger.LogInformation("Rolled {Roll} out of possible 10", roll);

        if (roll > _options.Value.ChanceOutOf10 && !Debugger.IsAttached)
        {
            _logger.LogInformation("doing nothing due to roll.");
            return;
        }

        var currentDate = DateTimeOffset.Now;
        var sleepTimeStart = new DateTimeOffset(currentDate.Year, currentDate.Month, currentDate.Day, 22, 0, 0, currentDate.Offset); // 10 PM
        var sleepTimeEnd = new DateTimeOffset(currentDate.Year, currentDate.Month, currentDate.Day, 9, 0, 0, currentDate.Offset); // 9 AM
        if (currentDate > sleepTimeStart || currentDate < sleepTimeEnd)
        {
            _logger.LogInformation("Bot is between sleep hours of {StartTime:T} and {EndTime:T} and will not send messages.", sleepTimeStart, sleepTimeEnd);
            return;
        }

        var registrations = _db.GetGuildChannelRegistrations();

        try
        {
            var exceptions = new List<Exception>();
            foreach (var reg in registrations)
            {
                var guild = await _client
                    .GetGuildAsync(reg.GuildId);

                var channel = await guild
                    .GetTextChannelAsync(reg.ChannelId);

                if (guild == null || channel == null)
                {
                    exceptions.Add(new ChannelNotFoundException("Channel not found", reg.ChannelId));
                    continue;
                }

                var messages = (await channel!.GetMessagesAsync(20).FlattenAsync())
                    .OrderByDescending(c => c.Timestamp)
                    .ToList();

                if (messages == null || !messages.Any())
                {
                    _logger.LogInformation("No messages found for Guild {GuildId} in Channel {ChannelId}", guild.Id, channel.Id);
                    continue;
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
                        continue;
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
                    Content = $"The previous {messages.Count} messages were from the 'Shitchat' discord server. " +
                    $"The messages are not necessarily directed at you. Please write a message as Alex " +
                    $"that makes sense within the context of the conversation." +
                    $"Do not start your response with \"Alex:\". You are posting to a Discord server and you do not need to state your name.",
                    Timestamp = DateTimeOffset.Now
                });

                // Re-order messages to be ascending according to timestamp
                chatGptMessages = chatGptMessages
                    .OrderBy(c => c.Timestamp)
                    .ToList();

                var response = await OpenAiService.ChatGpt(chatGptMessages);
                
                if (response.success)
                {
                    await channel.SendMessageAsync(response.responseMessage.Content);
                    _logger.LogInformation("For Guild {GuildId} in Channel {ChannelId}, Sent message: {ResponseMessage}", guild.Id, channel.Id, response.responseMessage.Content);

                    _cache.Set<ulong>(cacheKey, messages.First().Id);
                }
                else
                {
                    _logger.LogError("Failed to send response due to error from OpenAI: {Error}", response.responseMessage.Content);
                }
            }

            if (exceptions.Count > 0)
            {
                throw new AggregateException(exceptions);
            }
        }
        catch (AggregateException ae)
        {
            ae.Handle((ex) =>
            {
                if (ex is ChannelNotFoundException)
                {
                    _logger.LogWarning("Deleting {Channel} due to {Exception}", (ex as ChannelNotFoundException)?.ChannelId, nameof(ChannelNotFoundException));
                    var toDelete = registrations.Single(r => r.ChannelId == (ex as ChannelNotFoundException)?.ChannelId);
                    _db.DeleteGuildChannelRegistration(toDelete.GuildId, toDelete.ChannelId);
                    return true;
                }

                return false;
            });
        }
    }
    
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("{Service} is stopping.", nameof(TimedHostedService));

        _timer?.Change(Timeout.Infinite, 0);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
