﻿namespace DiscordChatGPT.Daemon.Options;

public class OpenAiOptions
{
    public string Model { get; set; } = "gpt-4";
    public string BaseUrl { get; set; } = "https://api.openai.com/v1/";
    public string ChatGptApiUrl { get; set; } = "https://api.openai.com/v1/chat/completions";
    public string DalleApiUrl { get; set; } = "https://api.openai.com/v1/images/generations";
}
