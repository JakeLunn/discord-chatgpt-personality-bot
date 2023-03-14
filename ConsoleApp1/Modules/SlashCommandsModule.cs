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
    private readonly BotOrchestrator _botOrchestrator;

    public SlashCommandsModule(IMemoryCache cache, 
        ILogger<SlashCommandsModule> logger, 
        DataService dataService,
        BotOrchestrator botOrchestrator)
    {
        _cache = cache;
        _logger = logger;
        _dataService = dataService;
        _botOrchestrator = botOrchestrator;
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
}