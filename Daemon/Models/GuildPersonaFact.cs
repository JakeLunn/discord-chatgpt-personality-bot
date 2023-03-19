using LiteDB;

namespace DiscordChatGPT.Daemon.Models;

public class GuildPersonaFact
{
    [BsonId(autoId: true)]
    public int Id { get; set; }
    public ulong GuildId { get; set; }
    public string Fact { get; set; } = string.Empty;
}
