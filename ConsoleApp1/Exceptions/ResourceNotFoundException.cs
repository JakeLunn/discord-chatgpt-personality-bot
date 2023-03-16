namespace DiscordChatGPT.Exceptions;

public class ResourceNotFoundException : Exception
{
    public Type ResourceType { get; }
    public ulong ResourceId { get; set; }

    public ResourceNotFoundException(string message, Type resourceType, ulong resourceId) : base(message)
    {
        ResourceId = resourceId;
        ResourceType = resourceType;
    }

    public ResourceNotFoundException(Type resourceType, ulong resourceId) : base($"Failed to find {resourceType.Name} with provided Id: {resourceId}")
    {
        ResourceType = resourceType;
        ResourceId = resourceId;
    }
}
