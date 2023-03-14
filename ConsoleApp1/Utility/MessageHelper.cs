using System.Text;
using System.Text.RegularExpressions;

namespace DiscordChatGPT.Utility;

public static class MessageHelper
{
    private const string MasterEmoteGuildId = "1084611475282333696";
    
    public static string TransformForDiscord(this string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;
        
        foreach (Match match in Regex.Matches(input, @"(?<!<a):([a-zA-Z0-9]+?):(?!\d+?>)"))
        {
            input = input.Replace(match.Value, $"<a:{match.Groups[1].Value}:{MasterEmoteGuildId}>");
        }

        input = input.Trim('"');

        return input;
    }
}
