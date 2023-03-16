﻿using Discord;
using Discord.Rest;
using Discord.WebSocket;
using DiscordChatGPT.Exceptions;
using DiscordChatGPT.Models;

namespace DiscordChatGPT.Factories;

public class ChatGPTMessagesFactory
{
    private readonly DiscordRestClient _restClient;

    private ChatGPTMessage? _startPrompt = null;
    private ChatGPTMessage? _tailPrompt = null;
    private List<ChatGPTMessage> _messages;

    public ChatGPTMessagesFactory(DiscordRestClient restClient)
    {
        _messages = new List<ChatGPTMessage>();
        _restClient = restClient;
    }

    public ChatGPTMessagesFactory WithPrompt(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentNullException(nameof(prompt));
        }

        _startPrompt = new ChatGPTMessage(ChatGPTRole.system, prompt, DateTimeOffset.MinValue);

        return this;
    }

    public ChatGPTMessagesFactory WithTailPrompt(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentNullException(nameof(prompt));
        }

        _tailPrompt = new ChatGPTMessage(ChatGPTRole.system, prompt);

        return this;
    }

    public ChatGPTMessagesFactory FromChannel(ulong guildId, ulong channelId, int limit = 20)
        => FromChannelAsync(guildId, channelId, limit).GetAwaiter().GetResult();

    public ChatGPTMessagesFactory FromChannel(ISocketMessageChannel channel, int limit = 20)
        => FromChannelAsync(channel, limit).GetAwaiter().GetResult();

    private async Task<ChatGPTMessagesFactory> FromChannelAsync(ulong guildId, ulong channelId, int limit)
    {
        var guild = await _restClient.GetGuildAsync(guildId);
        if (guild == null)
        {
            throw new ResourceNotFoundException(typeof(RestGuild), guildId);
        }

        var channel = await guild.GetTextChannelAsync(channelId);
        if (channel == null)
        {
            throw new ResourceNotFoundException(typeof(RestTextChannel), channelId);
        }

        return await FromChannelAsync(channel, limit);
    }

    private async Task<ChatGPTMessagesFactory> FromChannelAsync(IMessageChannel channel, int limit)
    {
        var channelMessages = (await channel.GetMessagesAsync(limit)
            .FlattenAsync())
            .ToList();

        foreach (var message in channelMessages)
        {
            _messages.Add(new ChatGPTMessage(
                    message.Author.Id == _restClient.CurrentUser.Id ? ChatGPTRole.assistant : ChatGPTRole.user,
                    message.CleanContent));
        }

        return this;
    }

    

    public IList<ChatGPTMessage> Build()
    {
        var result = new List<ChatGPTMessage>();

        if (_startPrompt != null)
        {
            result.Add(_startPrompt);
        }

        if (_messages.Any())
        {
            result.AddRange(_messages.OrderBy(m => m.Timestamp));
        }

        if (_tailPrompt != null)
        {
            result.Add(_tailPrompt);
        }

        return result;
    }
}
