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
        using var _ = _logger.BeginScope("Get Emotes From Guild");

        if (_cache.TryGetValue<List<GuildEmote>>($"EmoteReplace:{guildId}", out var cachedResult))
        {
            _logger.LogInformation("Using cached result for \"{GuildId}\"", guildId);
            return cachedResult!;
        }

        var guild = await _restClient.GetGuildAsync(guildId);
        if (guild == null)
        {
            throw new ResourceNotFoundException(typeof(RestGuild), guildId);
        }

        var masterGuild = await _restClient.GetGuildAsync(_options.Value.MasterGuildId);
        if (masterGuild == null)
        {
            throw new ResourceNotFoundException(typeof(RestGuild), _options.Value.MasterGuildId);
        }

        var emotes = await guild.GetEmotesAsync();
        if (emotes == null)
        {
            _logger.LogWarning("No emotes found for Guild {GuildId}", guildId);
            emotes = new List<GuildEmote>();
        }

        var masterGuildEmotes = await masterGuild.GetEmotesAsync();
        if (masterGuildEmotes == null)
        {
            _logger.LogWarning("No emotes found for Guild {GuildId}", guildId);
            masterGuildEmotes = new List<GuildEmote>();
        }

        var combinedEmotes = new List<GuildEmote>();

        combinedEmotes.AddRange(emotes);
        combinedEmotes.AddRange(masterGuildEmotes);

        _logger.LogInformation("Caching result for \"{GuildId}\"", guildId);
        _cache
            .CreateEntry($"EmoteReplace:{guildId}")
            .SetValue(combinedEmotes)
            .SetAbsoluteExpiration(TimeSpan.FromHours(1));

        return combinedEmotes;
    }
}

