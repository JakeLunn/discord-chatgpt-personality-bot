using Daemon.Options;
using Daemon.Utility;
using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using DiscordChatGPT;
using DiscordChatGPT.Daemon.Models;
using DiscordChatGPT.Daemon.Options;
using DiscordChatGPT.Daemon.Orchestrators;
using DiscordChatGPT.Daemon.Startup;
using DiscordChatGPT.Daemon.Utility;
using DiscordChatGPT.Exceptions;
using DiscordChatGPT.Modules;
using DiscordChatGPT.Options;
using DiscordChatGPT.Services;
using DiscordChatGPT.Utility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Reflection;

using IHost host = Host.CreateDefaultBuilder(args)
.ConfigureAppConfiguration(config =>
{
    config
        .AddJsonFile("local.settings.json", true, true)
        .AddEnvironmentVariables();
})
.ConfigureServices((host, services) =>
{
    var socketConfig = new DiscordSocketConfig()
    {
        GatewayIntents = GatewayIntents.MessageContent | GatewayIntents.AllUnprivileged
    };

    services
        .AddMemoryCache()
        .AddSingleton(socketConfig)
        .AddSingleton<SlashCommandsModule>()
        .AddSingleton(new DiscordSocketClient(socketConfig))
        .AddSingleton<DiscordRestClient>()
        .AddSingleton<DataAccessor>()
        .AddScoped<OpenAiAccessor>()
        .AddHostedService<TimedHostedService>();

    services
        .AddScopedInNamespace("DiscordChatGPT.Daemon.Orchestrators");

    services
        .AddHttpClient<OpenAiAccessor>()
        .AddPolicyHandler(HttpPolicies.GetRetryPolicy());

    services
        .Configure<TimedHostOptions>(host.Configuration.GetSection(nameof(TimedHostOptions)))
        .Configure<DataServiceOptions>(host.Configuration.GetSection(nameof(DataServiceOptions)))
        .Configure<GlobalDiscordOptions>(host.Configuration.GetSection(nameof(GlobalDiscordOptions)))
        .Configure<OpenAiOptions>(host.Configuration.GetSection(nameof(OpenAiOptions)))
        .Configure<Secrets>(host.Configuration.GetSection(nameof(Secrets)));

    services.AddLogging(builder =>
    {
        builder.AddSimpleConsole(options =>
        {
            options.IncludeScopes = true;
            options.SingleLine = true;
            options.TimestampFormat = "MM/dd hh:mm:ss tt ";
        });

        if (!Debugger.IsAttached)
        {
            builder.AddFile("DiscordChatGPT{Date}.log");
        }
    });
})
.Build();

await ServiceLifetime(host.Services);

await host.RunAsync();

static async Task ServiceLifetime(IServiceProvider serviceProvider)
{
    var socketClient = serviceProvider.GetRequiredService<DiscordSocketClient>();
    var restClient = serviceProvider.GetRequiredService<DiscordRestClient>();
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

    if (EnvironmentExtensions.TryGetEnvironmentVariable("GPTBOT_VERSION", out var version))
    {
        logger.LogInformation("BOT VERSION {VERSION}", version);
    }

    var interactionService = new InteractionService(socketClient);

    var addModulesTask = interactionService.AddModulesAsync(Assembly.GetEntryAssembly(), serviceProvider);

    var token = serviceProvider
        .GetRequiredService<IConfiguration>()
        .GetRequiredSection("Secrets")
        .GetRequiredValue<string>("DiscordToken");

    var restLoginTask = restClient.LoginAsync(Discord.TokenType.Bot, token);
    var socketLoginTask = socketClient.LoginAsync(Discord.TokenType.Bot, token);

    socketClient.Log += msg =>
    {
        switch (msg.Severity)
        {
            case Discord.LogSeverity.Critical:
            case Discord.LogSeverity.Error:
                logger.LogError(msg.Exception, msg.Message);
                break;
            case Discord.LogSeverity.Warning:
                logger.LogWarning(msg.Exception, msg.Message);
                break;
            case Discord.LogSeverity.Info:
                logger.LogInformation(msg.Exception, msg.Message);
                break;
            case Discord.LogSeverity.Verbose:
            case Discord.LogSeverity.Debug:
                logger.LogDebug(msg.Exception, msg.Message);
                break;
        }

        return Task.CompletedTask;
    };

    socketClient.SlashCommandExecuted += interaction =>
    {
        if (interaction.User.IsBot)
        {
            logger.LogDebug("Ignoring interaction from bot {BotName}", interaction.User.Username);
            return Task.CompletedTask;
        }

        var ctx = new SocketInteractionContext(socketClient, interaction);

        ThreadPool.QueueUserWorkItem(async _ =>
        {
            using (logger.BeginScope(interaction.Data.Name))
            {
                await interactionService.ExecuteCommandAsync(ctx, serviceProvider);
            }
        });

        return Task.CompletedTask;
    };

    socketClient.MessageReceived += message =>
    {
        if (message.Author.IsBot)
        {
            logger.LogDebug("Ignoring message from bot {BotName}", message.Author.Username);
            return Task.CompletedTask;
        }

        ThreadPool.QueueUserWorkItem(async _ =>
        {
            // Message is @ the bot
            if (message.MentionedUsers.Any(u => u.Id == socketClient.CurrentUser.Id))
            {
                using (var scope = serviceProvider.CreateScope())
                {
                    var orc = scope.ServiceProvider.GetRequiredService<BotOrchestrator>();
                    try
                    {
                        await orc.RespondToMentionAsync(message);
                    }
                    catch (ResourceNotFoundException rex)
                    {
                        if (!typeof(GuildPersonaFact).IsAssignableFrom(rex.ResourceType)) throw;

                        logger.LogWarning(rex, "Resetting facts for Guild {GuildId} to default.", rex.ResourceId);

                        var msgTask = message.Channel.SendMessageAsync($"{message.Author.Mention} Looks like this guild has no facts configured. I just added some default ones. " +
                            $"Go ahead and try your command again. If it doesn't work this time then idk.");

                        orc.ResetPersonaFactsToDefault(rex.ResourceId);

                        await msgTask;
                    }
                }
            }
        });

        return Task.CompletedTask;
    };

    socketClient.Ready += async () =>
    {
        await interactionService.RegisterCommandsGloballyAsync(true);
    };

    logger.LogInformation("Doing DB Connection Check");

    serviceProvider
        .GetRequiredService<DataAccessor>()
        .CheckConnection();

    logger.LogInformation("DB Connection Check successful");

    await Task.WhenAll(addModulesTask, restLoginTask, socketLoginTask);
    await socketClient.StartAsync();
}