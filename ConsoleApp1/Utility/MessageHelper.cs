using System.Text.RegularExpressions;

namespace DiscordChatGPT.Utility;

public static class MessageHelper
{
    private const string _masterEmoteGuildId = "1084611475282333696";

    private static readonly string[] _recognizedEmotes = new[]
    {
        "alienpls",
        "kekw",
        "aware"
    };
    
    public static string TransformForDiscord(this string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;
        
        foreach (Match match in Regex.Matches(input, @$"(?<!<a):({string.Join("|", _recognizedEmotes)}):(?!\d+?>)"))
        {
            input = input.Replace(match.Value, $"<a:{match.Groups[1].Value}:{_masterEmoteGuildId}>");
        }

        if (input.StartsWith('"') && input.EndsWith('"'))
        {
            input = input.Trim('"');
        }

        return input;
    }
}
