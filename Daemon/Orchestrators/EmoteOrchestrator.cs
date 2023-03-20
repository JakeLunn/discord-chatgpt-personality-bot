using Discord;
using Discord.Rest;
using DiscordChatGPT.Exceptions;
using DiscordChatGPT.Options;
using DiscordChatGPT.Utility;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace DiscordChatGPT.Daemon.Orchestrators;

public class EmoteOrchestrator
{
    private readonly DiscordRestClient _restClient;
    private readonly IOptions<GlobalDiscordOptions> _options;
    private readonly ILogger<EmoteOrchestrator> _logger;

    public EmoteOrchestrator(DiscordRestClient restClient,
        IOptions<GlobalDiscordOptions> options,
        ILogger<EmoteOrchestrator> logger)
    {
        _restClient = restClient;
        _options = options;
        _logger = logger;
    }

    public async Task<string> FormatDiscordMessageAsync(ulong guildId, string input)
    {
        (_, input) = await ReplaceEmotesAsync(guildId, input);
        input = input.TrimQuotations();

        return input;
    }

    public async Task<(int replacedCount, string result)> ReplaceEmotesAsync(ulong guildId, string input)
    {
        using var _ = _logger.BeginScope("Replace Emotes");

        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentNullException(nameof(input));
        }

        var masterGuild = await _restClient.GetGuildAsync(_options.Value.MasterGuildId);
        if (masterGuild == null)
        {
            throw new InvalidOperationException($"Failed to find master guild {_options.Value.MasterGuildId}");
        }

        var guild = await _restClient.GetGuildAsync(guildId);
        if (guild == null)
        {
            throw new ResourceNotFoundException(typeof(RestGuild), guildId);
        }

        var masterGuildEmotes = await masterGuild.GetEmotesAsync();
        if (masterGuildEmotes == null)
        {
            _logger.LogWarning("No emotes were found for the Master Guild {MasterGuildId}", _options.Value.MasterGuildId);
            return (0, input);
        }

        var emotes = await guild.GetEmotesAsync();
        if (emotes == null)
        {
            _logger.LogWarning("No emotes found for Guild {GuildId}", guildId);
            return (0, input);
        }

        var combinedEmoteList = new List<Emote>();

        combinedEmoteList.AddRange(masterGuildEmotes);
        combinedEmoteList.AddRange(combinedEmoteList);

        var count = 0;
        var matches = Regex.Matches(input, @$"(?<!<a):({string.Join("|", combinedEmoteList.Select(e => e.Name).ToArray())}):(?!\d+?>)");

        foreach (Match match in matches.Cast<Match>())
        {
            var emote = combinedEmoteList.FirstOrDefault(e => e.Name == match.Groups[1].Value);
            if (emote != null)
            {
                var emoteText = $"<a:{emote.Name}:{emote.Id}>";
                _logger.LogInformation("Replacing instances of \"{MatchedText}\" with \"{EmoteText}\"", match.Value, emoteText);
                input = Regex.Replace(input, @$"(?<!<a):({match.Groups[1].Value}):(?!\d+?>)", emoteText);
                count++;
            }
        }

        _logger.LogInformation("Replaced {Count} Emotes", count);

        return (count, input);
    }
}
