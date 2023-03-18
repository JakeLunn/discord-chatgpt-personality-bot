using DiscordChatGPT.Models;
using DiscordChatGPT.Options;
using LiteDB;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        using var db = new LiteDatabase(_options.Value.DatabasePath);

        var collection = db.GetCollection<GuildChannelRegistration>();

        collection.EnsureIndex(x => x.GuildId);

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
        using var db = new LiteDatabase(_options.Value.DatabasePath);

        var collection = db.GetCollection<GuildChannelRegistration>();

        collection.EnsureIndex(x => x.GuildId);

        return collection.Delete($"{guildId}{channelId}");
    }

    public bool IsChannelRegistered(ulong guildId, ulong channelId)
        => GetRegistration(guildId, channelId) != null;

    public GuildChannelRegistration? GetRegistration(ulong guildId, ulong channelId)
    {
        using var db = new LiteDatabase(_options.Value.DatabasePath);

        var collection = db.GetCollection<GuildChannelRegistration>();

        collection.EnsureIndex(x => x.GuildId);

        var registration = collection.FindOne(x => x.GuildId == guildId && x.ChannelId == channelId);

        return registration;
    }

    public IList<GuildChannelRegistration> GetGuildChannelRegistrations()
    {
        using var db = new LiteDatabase(_options.Value.DatabasePath);

        var collection = db.GetCollection<GuildChannelRegistration>();

        collection.EnsureIndex(x => x.GuildId);

        return collection
            .FindAll()
            .ToList();
    }
}
