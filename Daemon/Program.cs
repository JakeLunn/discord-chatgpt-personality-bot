using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using DiscordChatGPT;
using DiscordChatGPT.Daemon.Models;
using DiscordChatGPT.Daemon.Options;
using DiscordChatGPT.Daemon.Orchestrators;
using DiscordChatGPT.Daemon.Utility;
using DiscordChatGPT.Exceptions;
using DiscordChatGPT.Modules;
using DiscordChatGPT.Options;
using DiscordChatGPT.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Reflection;

using IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(config =>
    {
        config
            .AddJsonFile("local.settings.json", true)
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
            .AddSingleton<OpenAiAccessor>()
            .AddSingleton<BotOrchestrator>()
            .AddSingleton<EmoteOrchestrator>()
            .AddHostedService<TimedHostedService>()
            .Configure<DataServiceOptions>(host.Configuration.GetSection(nameof(DataServiceOptions)))
            .Configure<TimedHostOptions>(host.Configuration.GetSection(nameof(TimedHostOptions)))
            .Configure<GlobalDiscordOptions>(host.Configuration.GetSection(nameof(GlobalDiscordOptions)))
            .Configure<OpenAiOptions>(host.Configuration.GetSection(nameof(OpenAiOptions)));

        services.AddHttpClient<OpenAiAccessor>(c =>
        {
            var options = new OpenAiOptions();
            host.Configuration.GetSection(nameof(OpenAiOptions)).Bind(options);

            if (string.IsNullOrWhiteSpace(options.ApiKey))
            {
                throw new InvalidOperationException($"Application can't start without {nameof(options.ApiKey)}");
            }

            if (string.IsNullOrWhiteSpace(options.BaseUrl))
            {
                throw new InvalidOperationException($"Application can't start without {nameof(options.BaseUrl)}");
            }

            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
            c.BaseAddress = new Uri(options.BaseUrl);
        }).AddPolicyHandler(HttpPolicies.GetRetryPolicy());

        services.AddLogging(builder =>
        {
            builder.AddSimpleConsole(options =>
            {
                options.IncludeScopes = true;
                options.SingleLine = true;
                options.TimestampFormat = "MM/dd hh:mm:ss tt ";
            });

            builder.AddFile("DiscordChatGPT{Date}");
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

    var interactionService = new InteractionService(socketClient);

    await interactionService.AddModulesAsync(Assembly.GetEntryAssembly(), serviceProvider);

    socketClient.Log += async (msg) =>
    {
        if (msg.Severity == Discord.LogSeverity.Error)
        {
            logger.LogError(msg.Exception, msg.Message);
        }

        logger.LogInformation(msg.Message);
        await Task.CompletedTask;
    };

    socketClient.SlashCommandExecuted += async interaction =>
    {
        var ctx = new SocketInteractionContext(socketClient, interaction);
        using (logger.BeginScope(interaction.Data.Name))
        {
            await interactionService.ExecuteCommandAsync(ctx, serviceProvider);
        }
    };

    socketClient.MessageReceived += async message =>
    {
        // Message is @ the bot
        if (message.MentionedUsers.Any(u => u.Id == socketClient.CurrentUser.Id))
        {
            var orc = serviceProvider.GetRequiredService<BotOrchestrator>();
            try
            {
                await orc.RespondToMentionAsync(message);
            }
            catch (ResourceNotFoundException rex)
            {
                if (!typeof(GuildPersonaFact).IsAssignableFrom(rex.ResourceType)) throw;

                logger.LogWarning(rex, "Resetting facts for Guild {GuildId} to default.", rex.ResourceId);
                orc.ResetPersonaFactsToDefault(rex.ResourceId);
                await message.Channel.SendMessageAsync($"{message.Author.Mention} Looks like this guild has no facts configured. I just added some default ones." +
                    $"Go ahead and try your command again. If it doesn't work this time then idk.");
            }
        }

        await Task.CompletedTask;
    };

    socketClient.Ready += async () =>
    {
        await interactionService.RegisterCommandsGloballyAsync(true);
    };

    var config = serviceProvider.GetRequiredService<IConfiguration>();

    var token = config["GlobalDiscordOptions:Token"];
    if (token == null)
    {
        throw new InvalidOperationException("Discord Token was not found");
    }

    await restClient.LoginAsync(Discord.TokenType.Bot, token);
    await socketClient.LoginAsync(Discord.TokenType.Bot, token);

    logger.LogInformation("Doing DB Connection Check");

    serviceProvider
        .GetRequiredService<DataAccessor>()
        .CheckConnection();

    logger.LogInformation("DB Connection Check successful");
    
    logger.LogInformation("Initial Configuration Values Loaded:\n{Configuration}", string.Join("\n", config.AsEnumerable().OrderBy(a => a.Key)));

    await socketClient.StartAsync();
}