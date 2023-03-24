using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace DiscordChatGPT.Daemon.Startup;

internal static class ConfigureServicesExtensions
{
    internal static IServiceCollection AddScopedInNamespace(this IServiceCollection services, string targetNamespace)
    {
        var assembly = Assembly.GetExecutingAssembly();

        var typesToRegister = assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Namespace == targetNamespace)
            .ToList();

        foreach (var type in typesToRegister)
        {
            var interfaces = type.GetInterfaces();
            if (interfaces.Any())
            {
                services.AddScoped(interfaces.Single(), type);
                continue;
            }

            services.AddScoped(type);
        }

        return services;
    }
}
