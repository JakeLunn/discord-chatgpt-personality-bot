using Discord;
using Discord.Rest;
using DiscordChatGPT.Exceptions;
using DiscordChatGPT.Options;
using DiscordChatGPT.Utility;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace DiscordChatGPT.Daemon.Orchestrators;

public class EmoteOrchestrator
{
    private readonly DiscordRestClient _restClient;
    private readonly IOptions<GlobalDiscordOptions> _options;
    private readonly IMemoryCache _cache;
    private readonly ILogger<EmoteOrchestrator> _logger;

    public EmoteOrchestrator(DiscordRestClient restClient,
        IOptions<GlobalDiscordOptions> options,
        IMemoryCache cache,
        ILogger<EmoteOrchestrator> logger)
    {
        _restClient = restClient;
        _options = options;
        _cache = cache;
        _logger = logger;
    }

    public async Task<string> FormatDiscordMessageAsync(ulong guildId, string input)
    {
        input = input.TrimQuotations();
        input = TrimUsernameStart(input);
        input = RemoveNumberStrings(input);
        (_, input) = await ReplaceEmotesAsync(guildId, input);

        return input;
    }

    private string TrimUsernameStart(string input)
    {
        var username = $"{_restClient.CurrentUser.Username}:";
        if (!input.StartsWith(username))
        {
            _logger.LogInformation("Input does not start with username \"{Username}\"", username);
            return input;
        }

        _logger.LogInformation("Removing username \"{Username}\" from start of input", username);
        input = input[username.Length..];

        return input;
    }

    private string RemoveNumberStrings(string input)
    {
        var regex = @"\d{8,22}";
        return Regex.Replace(input, regex, string.Empty);
    }

    public async Task<(int replacedCount, string result)> ReplaceEmotesAsync(ulong guildId, string input)
    {
        using var _ = _logger.BeginScope("Replace Emotes");

        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentNullException(nameof(input));
        }

        var combinedEmoteList = await GetEmotesAsync(guildId);

        var count = 0;
        var regex = @$"(?<!<a):({string.Join("|", combinedEmoteList.Select(e => e.Name).ToArray())}):(?!\d+?>)";

        _logger.LogInformation("Searching for emotes matching regex \"{Regex}\"", regex);
        var matches = Regex.Matches(input, regex);

        foreach (Match match in matches.Cast<Match>())
        {
            _logger.LogInformation("Processing match for \"{MatchedText}\"", match.Value);
            var emote = combinedEmoteList.FirstOrDefault(e => e.Name == match.Groups[1].Value);
            if (emote != null)
            {
                var emoteText = $"<{(emote.Animated ? "a" : string.Empty)}:{emote.Name}:{emote.Id}>";
                _logger.LogInformation("Replacing instances of \"{MatchedText}\" with \"{EmoteText}\"", match.Value, emoteText);
                input = Regex.Replace(input, @$"(?<!<a):({match.Groups[1].Value}):(?!\d+?>)", emoteText);
                count++;
            }
        }

        _logger.LogInformation("Replaced {Count} Emotes", count);

        return (count, input);
    }

    private async Task<List<GuildEmote>> GetEmotesAsync(ulong guildId)
    {
        var getGuildEmotesTask = GetEmotesForGuildAsync(guildId);
        var getMasterGuildEmotesTask = GetEmotesForGuildAsync(_options.Value.MasterGuildId);

        await Task.WhenAll(getGuildEmotesTask, getMasterGuildEmotesTask);

        return new List<GuildEmote>((await getGuildEmotesTask).Concat(await getMasterGuildEmotesTask));
    }

    private async Task<List<GuildEmote>> GetEmotesForGuildAsync(ulong guildId)
    {
        var emotes = await _cache.GetOrCreateAsync($"EmoteReplace:{guildId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);

            var guild = await _restClient.GetGuildAsync(guildId);
            if (guild == null)
            {
                throw new ResourceNotFoundException(typeof(RestGuild), guildId);
            }

            var emotes = (await guild.GetEmotesAsync()).ToList();
            if (emotes == null)
            {
                _logger.LogWarning("No emotes found for Guild {GuildId}", guildId);
                emotes = new List<GuildEmote>();
            }

            return emotes;
        });

        return emotes!;
    }
}

