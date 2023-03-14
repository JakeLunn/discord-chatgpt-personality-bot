using System.Data;
using System.Net.NetworkInformation;
using System.Reactive;
using System.Text.RegularExpressions;
using Discord;
using Discord.Interactions;
using DiscordChatGPT.Models;
using DiscordChatGPT.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace DiscordChatGPT.Modules;

public class SlashCommandsModule : InteractionModuleBase
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<SlashCommandsModule> _logger;
    private readonly DataService _dataService;

    public SlashCommandsModule(IMemoryCache cache, ILogger<SlashCommandsModule> logger, DataService dataService)
    {
        _cache = cache;
        _logger = logger;
        _dataService = dataService;
    }

    [SlashCommand("ping",
        "This is a basic ping command to check if the Bot is online and what the current Latency is")]
    public async Task PingSlashCommand()
    {
        // Creating the ping to measure response time
        var pinger = new Ping();

        // Creating a Message in the channel
        await Context.Channel.SendMessageAsync("Ping...");

        // Starts the Response with a thinking state
        await DeferAsync();

        // Stop the stopwatch and output the elapsed time
        var reply = pinger.Send("google.com");

        var embed = new EmbedBuilder()
            .WithTitle("Pong!")
            .WithDescription($"Latency is: {reply.RoundtripTime} ms")
            .Build();

        // Sending the Embed Message to the Channel
        await Context.Channel.SendMessageAsync(embed: embed);

        // Deleting the thinking state
        await DeleteOriginalResponseAsync();
    }

    [SlashCommand("register", "Register the current channel for the bot to be active in.")]
    public async Task RegisterChannel()
    {
        await DeferAsync(true);

        _dataService.AddGuildChannelRegistration(new GuildChannelRegistration(Context.Guild.Id, Context.Channel.Id));

        await ModifyOriginalResponseAsync(m =>
        {
            m.Content = "Channel registered successfully. To unregister this channel, try /unregister";
            m.Flags = MessageFlags.None;
        });
    }

    [SlashCommand("unregister", "Unregister the current channel for the bot.")]
    public async Task UnregisterChannel()
    {
        await DeferAsync(true);

        _dataService.DeleteGuildChannelRegistration(Context.Guild.Id, Context.Channel.Id);

        await ModifyOriginalResponseAsync(m =>
        {
            m.Content = "Unregistered channel successfully. To re-register channel, try /register";
            m.Flags = MessageFlags.None;
        });
    }

    [SlashCommand("chatgpt",
        "Send a custom Text to the OpenAI - ChatGPT API and get a response from their AI based on your input")]
    public async Task ChatSlashCommand([Summary("prompt", "Write an input that the ChatGPT AI should respond to")]
        string text)
    {
        // Creating a Message in the channel
        await Context.Channel.SendMessageAsync($"Request from {Context.User.Username}: {text}");

        await DeferAsync();

        var cacheKey = $"{Context.Guild.Id}{Context.Channel.Id}";
        _cache.TryGetValue<List<ChatGPTMessage>>(cacheKey, out var messages);

        var message = new ChatGPTMessage
        {
            Role = "user",
            Content = text,
            Timestamp = DateTimeOffset.UtcNow
        };
        
        if (messages == null)
        {
            messages = new List<ChatGPTMessage> { message };
            
            // Starting Prompt
            messages.Add(new ChatGPTMessage
            {
                Role = "system",
                Content = Constants.StartingPromptText,
                Timestamp = DateTimeOffset.MinValue
            });
        }
        else
        {
            messages.Add(message);
        }

        messages = messages.OrderBy(m => m.Timestamp).ToList();

        // Execute and waiting for the response from our Method
        var (success, responseMessage) = await OpenAiService.ChatGpt(messages);

        // Log if anything goes wrong while executing the request
        if (!success) _logger.LogWarning(responseMessage.Content);

        await DeleteOriginalResponseAsync();
        await Context.Channel.SendMessageAsync(responseMessage.Content);

        messages.Add(responseMessage);

        _cache.Set(cacheKey, messages, TimeSpan.FromMinutes(Constants.MinutesToSaveConversation));
    }

    [SlashCommand("dalle",
        "Send a custom Text to the OpenAI - DALL-E Api and get a generated Image based on input")]
    public async Task ImageSlashCommand(
        [Summary("prompt", "Write a Text on how the generated Image should look like")]
        string text)
    {
        // Send a message indicating that the command is being executed
        await Context.Channel.SendMessageAsync($"Request from {Context.User.Username}: {text}");

        // Send a "thinking" response to let the user know that the bot is working on their request
        await DeferAsync();

        // Execute the DALL-E API request and wait for a response
        var (sucess, message) = await OpenAiService.DallE(text);

        // Extract the image URL from the response message using a regular expression
        var url = Regex.Match(message, @"http(s)?://([\w-]+\.)+[\w-]+(/[\w- ./?%&=]*)?").ToString();

        // Create an embed message to display the generated image
        var embed = new EmbedBuilder()
            .WithTitle("DALL-E")
            .WithDescription($"Prompt: {text}")
            .WithImageUrl(url)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .Build();

        // If the API request was not successful, log the error message
        if (!sucess) _logger.LogWarning(message);

        // Send the embed message with the generated image to the channel
        await DeleteOriginalResponseAsync();
        await Context.Channel.SendMessageAsync(embed: embed);
    }
}