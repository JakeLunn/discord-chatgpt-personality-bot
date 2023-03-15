using Discord.Rest;
using DiscordChatGPT.Exceptions;
using DiscordChatGPT.Options;
using DiscordChatGPT.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace DiscordChatGPT;

public class TimedHostedService : IHostedService, IDisposable
{
    private int executionCount = 0;
    private readonly ILogger<TimedHostedService> _logger;
    private readonly DiscordRestClient _client;
    private readonly IMemoryCache _cache;
    private readonly DataService _db;
    private readonly BotOrchestrator _botOrchestrator;
    private readonly IOptions<TimedHostOptions> _options;
    private readonly static Random _random = new();
    private Timer? _timer = null;

    public TimedHostedService(ILogger<TimedHostedService> logger,
        DiscordRestClient client,
        IMemoryCache cache,
        DataService db,
        BotOrchestrator botOrchestrator,
        IOptions<TimedHostOptions> options)
    {
        _logger = logger;
        _client = client;
        _cache = cache;
        _db = db;
        _botOrchestrator = botOrchestrator;
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

        var currentDate = DateTime.Now;
        var sleepTimeStart = new TimeSpan(22, 0, 0);
        var sleepTimeEnd = new TimeSpan(9, 0, 0);
        if (currentDate.TimeOfDay > sleepTimeStart && currentDate.TimeOfDay < sleepTimeEnd)
        {
            _logger.LogInformation("Bot is between sleep hours of {StartTime:c} and {EndTime:c} and will not send messages.", sleepTimeStart, sleepTimeEnd);
            return;
        }

        var registrations = _db.GetGuildChannelRegistrations();

        try
        {
            var exceptions = new List<Exception>();
            foreach (var reg in registrations)
            {
                try
                {
                    await _botOrchestrator.RespondToChannelAsync(reg.GuildId, reg.ChannelId);
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
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
