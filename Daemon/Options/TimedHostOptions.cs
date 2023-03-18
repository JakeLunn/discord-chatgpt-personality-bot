namespace DiscordChatGPT.Options;

public class TimedHostOptions
{
    public string TimerTimeSpan { get; set; } = "00:15:00";
    public string SleepStartTimeSpan { get; set; } = "22:00:00";
    public string SleepEndTimeSpan { get; set; } = "09:00:00";
    public int ChanceOutOf100 { get; set; } = 30;
}
