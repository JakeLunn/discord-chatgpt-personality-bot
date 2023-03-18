using Discord;
using Discord.Interactions;
using DiscordChatGPT.Models;
using DiscordChatGPT.Services;

namespace DiscordChatGPT.Modules;

public class SlashCommandsModule : InteractionModuleBase
{
    private readonly DataService _dataService;

    public SlashCommandsModule(DataService dataService)
    {
        _dataService = dataService;
    }

    [SlashCommand("register", "Register the current channel for the bot to be active in. Bot will only work in registered channels.")]
    public async Task RegisterChannel()
    {
        await DeferAsync();

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
        await DeferAsync();

        _dataService.DeleteGuildChannelRegistration(Context.Guild.Id, Context.Channel.Id);

        await ModifyOriginalResponseAsync(m =>
        {
            m.Content = "Unregistered channel successfully. To re-register channel, try /register";
            m.Flags = MessageFlags.None;
        });
    }
}