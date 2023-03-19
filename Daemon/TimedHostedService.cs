using Discord.Rest;
using DiscordChatGPT.Daemon.Models;
using DiscordChatGPT.Daemon.Orchestrators;
using DiscordChatGPT.Exceptions;
using DiscordChatGPT.Options;
using DiscordChatGPT.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordChatGPT;

public class TimedHostedService : IHostedService, IDisposable
{
    private int executionCount = 0;
    private readonly ILogger<TimedHostedService> _logger;
    private readonly DiscordRestClient _client;
    private readonly IMemoryCache _cache;
    private readonly DataAccessor _db;
    private readonly BotOrchestrator _botOrchestrator;
    private TimedHostOptions _options;
    private readonly static Random _random = new();

    private Timer? _timer = null;
    private IDisposable? _changeMonitor = null;

    public TimedHostedService(ILogger<TimedHostedService> logger,
        DiscordRestClient client,
        IMemoryCache cache,
        DataAccessor db,
        BotOrchestrator botOrchestrator,
        IOptionsMonitor<TimedHostOptions> options)
    {
        _logger = logger;
        _client = client;
        _cache = cache;
        _db = db;
        _botOrchestrator = botOrchestrator;
        _options = options.CurrentValue;

        _changeMonitor = options.OnChange(OnOptionsChange);
    }

    private void OnOptionsChange(TimedHostOptions newOptions)
    {
        using var _ = _logger.BeginScope("Options Monitor Change");

        _logger.LogInformation("Updating options.");

        if (newOptions.TimerTimeSpan != _options.TimerTimeSpan)
        {
            if (TimeSpan.TryParse(newOptions.TimerTimeSpan, out var newTimeSpan))
            {
                _logger.LogInformation("Updating timer to {TimeSpan}", newOptions.TimerTimeSpan);
                _timer?.Change(TimeSpan.Zero, newTimeSpan);
            }
            else
            {
                _logger.LogWarning("New timer value {TimeSpan} was unable to be parsed and will not be applied.", newOptions.TimerTimeSpan);
            }
        }

        _options = newOptions;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("{Service} has started", nameof(TimedHostedService));

        _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.Parse(_options.TimerTimeSpan));

        return Task.CompletedTask;
    }

    private void DoWork(object? state)
        => DoWorkAsync(state).GetAwaiter().GetResult();

    private async Task DoWorkAsync(object? state)
    {
        Interlocked.Increment(ref executionCount);

        _logger.LogInformation(
            "{Service} is working. Count: {Count}", nameof(TimedHostedService), executionCount);

        var currentDate = DateTime.Now;
        var sleepTimeStart = TimeSpan.Parse(_options.SleepStartTimeSpan);
        var sleepTimeEnd = TimeSpan.Parse(_options.SleepEndTimeSpan);

        if (currentDate.TimeOfDay > sleepTimeStart || currentDate.TimeOfDay < sleepTimeEnd)
        {
            _logger.LogInformation("Bot is between sleep hours of {StartTime:c} and {EndTime:c} and will not send messages.", sleepTimeStart, sleepTimeEnd);
            return;
        }

        var registrations = _db.GetGuildChannelRegistrations();
        _logger.LogInformation("Retrieved {Count} Guild Registrations", registrations.Count);

        try
        {
            var exceptions = new List<Exception>();
            foreach (var reg in registrations)
            {
                var chance = _options.ChanceOutOf100;
                var roll = _random.Next(1, 100);
                _logger.LogInformation("Rolled {Roll} out of possible 100. Target roll is <= {Target}.", roll, chance);

                // Given %N Chance, then a roll > N would fail and continue the loop.
                if (roll > chance)
                {
                    _logger.LogInformation("doing nothing due to roll.");
                    continue;
                }

                try
                {
                    await _botOrchestrator.RespondToGuildTextChannelAsync(reg.GuildId, reg.ChannelId);
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
                if (ex is ResourceNotFoundException rex)
                {
                    if (typeof(RestChannel).IsAssignableFrom(rex.ResourceType))
                    {
                        _logger.LogWarning(rex, "Deleting {Channel} due to {Exception}", rex.ResourceId, nameof(ResourceNotFoundException));
                        var toDelete = registrations.Single(r => r.ChannelId == rex.ResourceId);
                        _db.DeleteGuildChannelRegistration(toDelete.GuildId, toDelete.ChannelId);
                        return true;
                    }

                    if (typeof(GuildPersonaFact).IsAssignableFrom(rex.ResourceType))
                    {
                        _logger.LogWarning(rex, "Resetting facts for guild {GuildId} back to default", rex.ResourceId);
                        _botOrchestrator.ResetPersonaFactsToDefault(rex.ResourceId);
                        return true;
                    }
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
        _changeMonitor?.Dispose();
    }
}
