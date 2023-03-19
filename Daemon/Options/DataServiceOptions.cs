namespace DiscordChatGPT.Options;

public class DataServiceOptions
{
    public string Salt { get; set; } = string.Empty;
    public string DatabasePath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "LiteDB", "DiscordChatGPT.db");
}
