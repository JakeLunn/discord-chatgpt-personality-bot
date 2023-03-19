using DiscordChatGPT.Daemon.Models;
using DiscordChatGPT.Models;
using DiscordChatGPT.Options;
using LiteDB;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Linq.Expressions;

namespace DiscordChatGPT.Services;

public class DataAccessor
{
    private readonly IOptions<DataServiceOptions> _options;
    private readonly ILogger<DataAccessor> _logger;

    public DataAccessor(IOptions<DataServiceOptions> options,
        ILogger<DataAccessor> logger)
    {
        _options = options;
        _logger = logger;
    }

    public void CheckConnection()
    {
        using var db = new LiteDatabase(_options.Value.DatabasePath);

        var collections = db.GetCollectionNames();

        _logger.LogInformation("Database Connected... collections: {Collections}", string.Join(", ", collections));
    }

    public GuildChannelRegistration AddGuildChannelRegistration(GuildChannelRegistration reg)
    {
        using var db = OpenCollection<GuildChannelRegistration>(out var collection,
            x => x.GuildId,
            x => x.ChannelId);

        var existing = collection.FindOne(x => x.GuildId == reg.GuildId && x.ChannelId == reg.ChannelId);
        if (existing != null)
        {
            _logger.LogWarning("Guild Channel Registration already exists for {GuildId} and {ChannelId}", reg.GuildId, reg.ChannelId);
            return existing;
        }

        collection.Insert(reg);

        return reg;
    }

    public bool DeleteGuildChannelRegistration(ulong guildId, ulong channelId)
    {
        using var db = OpenCollection<GuildChannelRegistration>(out var collection, 
            x => x.GuildId,
            x => x.ChannelId);

        return collection.Delete($"{guildId}{channelId}");
    }

    public bool IsChannelRegistered(ulong guildId, ulong channelId)
        => GetRegistration(guildId, channelId) != null;

    public GuildChannelRegistration? GetRegistration(ulong guildId, ulong channelId)
    {
        using var db = OpenCollection<GuildChannelRegistration>(out var collection, x => x.GuildId);

        var registration = collection.FindOne(x => x.GuildId == guildId && x.ChannelId == channelId);

        return registration;
    }

    public IList<GuildChannelRegistration> GetGuildChannelRegistrations()
    {
        using var db = OpenCollection<GuildChannelRegistration>(out var collection, x => x.GuildId);

        return collection
            .FindAll()
            .ToList();
    }

    public GuildPersonaFact? GetPersonaFact(int id)
    {
        using var db = OpenCollection<GuildPersonaFact>(out var collection, x => x.GuildId);

        return collection.FindById(id);
    }

    public IList<GuildPersonaFact> GetPersonaFacts(ulong guildId)
    {
        using var db = OpenCollection<GuildPersonaFact>(out var collection, x => x.GuildId);

        return collection
            .Find(x => x.GuildId == guildId)
            .ToList();
    }

    public int InsertPersonaFact(GuildPersonaFact fact)
    {
        using var db = OpenCollection<GuildPersonaFact>(out var collection, 
            x => x.GuildId,
            x => x.Id);

        return collection.Insert(fact);
    }

    public int BulkInsertPersonaFacts(IList<GuildPersonaFact> facts)
    {
        using var db = OpenCollection<GuildPersonaFact>(out var collection,
            x => x.GuildId,
            x => x.Id);

        return collection.InsertBulk(facts);
    }

    public bool DeletePersonaFact(int factId)
    {
        using var db = OpenCollection<GuildPersonaFact>(out var collection, x => x.Id);
        return collection.Delete(factId);
    }

    public int DeleteAllPersonaFactsForGuild(ulong guildId)
    {
        using var db = OpenCollection<GuildPersonaFact>(out var collection, x => x.GuildId);
        return collection.DeleteMany(x => x.GuildId == guildId);
    }

    private LiteDatabase? OpenCollection<T>(out ILiteCollection<T> collection, params Expression<Func<T, object>>[] ensureIndexes)
    {
        var db = new LiteDatabase(_options.Value.DatabasePath);

        collection = db.GetCollection<T>();

        if (ensureIndexes.Length > 0)
        {
            foreach (var ensureIndex in ensureIndexes)
            {
                collection.EnsureIndex(ensureIndex);
            }
        }

        return db;
    }
}
