namespace DiscordChatGPT.Options;

public class DataServiceOptions
{
    public string DatabasePath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "LiteDB", "DiscordChatGPT.db");
}
