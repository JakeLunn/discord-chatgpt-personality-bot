using System.Text;

namespace DiscordChatGPT.Daemon.Factories;

public class ChatGPTPromptBuilder
{
    private IList<string> _facts = new List<string>();
    private string _name = Constants.DefaultName;

    public ChatGPTPromptBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public ChatGPTPromptBuilder WithFacts(List<string> facts)
    {
        _facts = facts;
        return this;
    }

    public string Build()
    {
        var sb = new StringBuilder($"You are a Discord user named {_name}. " +
            $"Never start your messages with \"{_name}:\". " +
            $"As {_name}, you must stricly follow these rules when responding to any future prompts:\n");

        foreach (var fact in _facts)
        {
            sb.AppendLine($"- {fact}");
        }

        return sb.ToString();
    }
}
