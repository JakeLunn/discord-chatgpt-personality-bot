using Discord;
using Discord.Interactions;
using DiscordChatGPT.Models;
using DiscordChatGPT.Services;

namespace DiscordChatGPT.Modules;

public class SlashCommandsModule : InteractionModuleBase
{
    private readonly DataAccessor _dataAccessor;

    public SlashCommandsModule(DataAccessor dataAccessor)
    {
        _dataAccessor = dataAccessor;
    }

    [SlashCommand("register", "Register the current channel for the bot to be active in. Bot will only work in registered channels.")]
    public async Task RegisterChannel()
    {
        await DeferAsync();

        var (result, _) = _dataAccessor.AddGuildChannelRegistration(new GuildChannelRegistration(Context.Guild.Id, Context.Channel.Id));

        if (result == DataResult.AlreadyExists)
        {
            await ModifyOriginalResponseAsync(m =>
            {
                m.Content = "Channel is already registered. To unregister channel, try /unregister";
                m.Flags = MessageFlags.None;
            });

            return;
        }

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

        var result = _dataAccessor.DeleteGuildChannelRegistration(Context.Guild.Id, Context.Channel.Id);
        if (result == DataResult.Success)
        {
            await ModifyOriginalResponseAsync(m =>
            {
                m.Content = "Unregistered channel successfully. To re-register channel, try /register";
                m.Flags = MessageFlags.None;
            });

            return;
        }

        await ModifyOriginalResponseAsync(m =>
        {
            m.Content = "Channel was already unregistered. To register channel, try /register";
            m.Flags = MessageFlags.None;
        });
    }
}