namespace DiscordChatGPT.Utility;

public static class StringExtensions
{
    public static string TrimQuotations(this string input)
    {
        if (input.StartsWith('"') && input.EndsWith('"'))
        {
            input = input.Trim('"');
        }

        return input;
    }
}
