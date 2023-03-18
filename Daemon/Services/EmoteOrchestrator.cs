using Discord.Rest;
using DiscordChatGPT.Exceptions;
using DiscordChatGPT.Options;
using DiscordChatGPT.Utility;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace DiscordChatGPT.Services;

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

    public string FormatDiscordMessage(string input)
        => FormatDiscordMessageAsync(input).GetAwaiter().GetResult();

    public string ReplaceEmotes(string input)
        => ReplaceEmotesAsync(input).GetAwaiter().GetResult();

    public async Task<string> FormatDiscordMessageAsync(string input)
    {
        input = await ReplaceEmotesAsync(input);
        input = input.TrimQuotations();

        return input;
    }

    public async Task<string> ReplaceEmotesAsync(string input)
    {
        var guildId = _options.Value.MasterGuildId;

        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentNullException(nameof(input));
        }

        var guild = await _restClient.GetGuildAsync(guildId);
        if (guild == null)
        {
            throw new ResourceNotFoundException(typeof(RestGuild), guildId);
        }

        var emotes = await guild.GetEmotesAsync();
        if (emotes == null)
        {
            _logger.LogWarning("No emotes found");
            return input;
        }

        foreach (Match match in Regex.Matches(input, @$"(?<!<a):({string.Join("|", emotes.Select(e => e.Name).ToArray())}):(?!\d+?>)"))
        {
            var emote = emotes.FirstOrDefault(e => e.Name == match.Groups[1].Value);
            if (emote != null)
            {
                input = input.Replace(match.Value, $"<a:{emote.Name}:{emote.Id}>");
            }
        }

        return input;
    }
}
