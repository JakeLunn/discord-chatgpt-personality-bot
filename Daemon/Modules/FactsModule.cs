using Discord;
using Discord.Interactions;
using DiscordChatGPT.Daemon.Models;
using DiscordChatGPT.Daemon.Orchestrators;
using DiscordChatGPT.Options;
using DiscordChatGPT.Services;
using HashidsNet;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace DiscordChatGPT.Daemon.Modules;

[Group("facts", "Modify the persona facts for this discord server")]
public class FactsModule : InteractionModuleBase
{
    private readonly DataAccessor _dataAccessor;
    private readonly BotOrchestrator _botOrchestrator;
    private readonly IMemoryCache _cache;
    private readonly Hashids _hashIds;

    public FactsModule(IOptions<DataServiceOptions> options,
        DataAccessor dataAccessor,
        BotOrchestrator botOrchestrator,
        IMemoryCache cache)
    {
        _dataAccessor = dataAccessor;
        _botOrchestrator = botOrchestrator;
        _cache = cache;
        _hashIds = new Hashids(options.Value.Salt);
    }

    [SlashCommand("reset", "Resets the persona back to the default set of facts")]
    public async Task ResetToDefaultAsync()
    {
        var deferTask = DeferAsync(true);

        _botOrchestrator.ResetPersonaFactsToDefault(Context.Guild.Id);

        _cache.Remove($"Facts|{Context.Guild.Id}");

        await deferTask;
        await ModifyOriginalResponseAsync(r =>
        {
            r.Content = "Facts have been reset to default. Use `/facts list` to see the new list.";
        });
    }

    [SlashCommand("list", "List the facts for the persona with their identifiers")]
    public async Task ListFactsAsync()
    {
        var deferTask = DeferAsync(true);

        if (!_cache.TryGetValue($"Facts|{Context.Guild.Id}", out IList<GuildPersonaFact>? facts))
        {
            facts = _dataAccessor.GetPersonaFacts(Context.Guild.Id);
        }

        var description = string.Join("\n",
            facts!.OrderBy(f => f.Id).Select(f => $"(**{_hashIds.Encode(f.Id)}**) -- {f.Fact}"));

        var embed = new EmbedBuilder()
            .WithTitle($"Personality Facts")
            .WithAuthor(Context.Client.CurrentUser.Username)
            .WithDescription(description)
            .WithThumbnailUrl(Context.Client.CurrentUser.GetAvatarUrl());

        await deferTask;
        await ModifyOriginalResponseAsync(r =>
        {
            r.Embed = embed.Build();
        });
    }

    [SlashCommand("add", "Add a new fact to the persona")]
    public async Task AddFactAsync(
        [Summary("Fact", "e.g. \"You are a fan of K-Pop\"")]
            string fact)
    {
        var deferTask = DeferAsync(true);

        var id = _dataAccessor.InsertPersonaFact(new GuildPersonaFact
        {
            GuildId = Context.Guild.Id,
            Fact = fact
        });

        _cache.Remove($"Facts|{Context.Guild.Id}");

        await deferTask;
        await ModifyOriginalResponseAsync(r =>
        {
            r.Content = $"Fact has been added with Id {_hashIds.Encode(id)}.";
        });
        
    }

    [SlashCommand("update", "Update an existing fact")]
    public async Task UpdateFactAsync(string id, string newFactText)
    {
        var deferTask = DeferAsync(true);
        var intId = _hashIds.Decode(id).Single();

        var fact = _dataAccessor.GetPersonaFact(intId);
        if (fact == null)
        {
            await deferTask;
            await ModifyOriginalResponseAsync(r =>
            {
                r.Content = $"Failed to find Fact {id}";
            });
            return;
        }

        fact.Fact = newFactText;
        if (!_dataAccessor.UpdatePersonaFact(fact))
        {
            await deferTask;
            await ModifyOriginalResponseAsync(r =>
            {
                r.Content = $"Failed to update Fact {id} due to an unknown error.";
            });
            return;
        }

        await deferTask;
        await ModifyOriginalResponseAsync(r =>
        {
            r.Content = $"Updated Fact {id} to: {newFactText}";
        });
    }

    [SlashCommand("delete", "Delete a fact for the persona")]
    public async Task RemoveFactAsync([Summary("Id", "The associated fact Id")] string id)
    {
        var deferTask = DeferAsync(true);
        var intId = _hashIds.Decode(id).Single();

        var fact = _dataAccessor.GetPersonaFact(intId);
        if (fact == null || fact.GuildId != Context.Guild.Id)
        {
            await deferTask;
            await ModifyOriginalResponseAsync(r =>
            {
                r.Content = $"A fact with Id {id} was not found.";
            });
            return;
        }

        var didDelete = _dataAccessor.DeletePersonaFact(intId);
        if (!didDelete)
        {
            await deferTask;
            await ModifyOriginalResponseAsync(r =>
            {
                r.Content = $"Failed to delete fact due to an unknown error.";
            });
            return;
        }

        _cache.Remove($"Facts|{Context.Guild.Id}");

        await deferTask;
        await ModifyOriginalResponseAsync(r =>
        {
            r.Content = $"Fact deleted.";
        });
    }
}