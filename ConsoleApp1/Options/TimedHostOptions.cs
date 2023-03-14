namespace DiscordChatGPT.Options;

public class TimedHostOptions
{
    public string TimedHostTimeSpan { get; set; } = "00:10:00";
    public int ChanceOutOf10 { get; set; } = 3;
}
