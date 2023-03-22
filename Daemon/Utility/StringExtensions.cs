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

public static class EnvironmentExtensions
{
    public static bool TryGetEnvironmentVariable(string name, out string? value)
    {
        value = Environment.GetEnvironmentVariable(name);
        return !string.IsNullOrWhiteSpace(value);
    }
}
