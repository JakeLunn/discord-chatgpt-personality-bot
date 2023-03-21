using Microsoft.Extensions.Configuration;

namespace Daemon.Utility;

public static class ConfigurationExtensions
{
    /// <summary>
    /// Gets a required value from the configuration section.
    /// </summary>
    public static T GetRequiredValue<T>(this IConfigurationSection section, string key)
    {
        var value = section.GetValue<T>(key);
        if (value == null)
        {
            throw new ConfigurationException($"Missing required configuration value: {section.Path}:{key}");
        }
        return value;
    }
}