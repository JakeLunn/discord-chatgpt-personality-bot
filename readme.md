# Summary
This is a discord bot written in C# which allows guilds to create a personality that they can interact with. The discord bot takes the user's message and sends it to Open AI's `v1/chat/completions` endpoint ([docs here](https://platform.openai.com/docs/api-reference/chat)). The bot will prepend the request with the most recent message history from the current channel (default 20 messages), as well as a system prompt that defines the character that GPT4 will roleplay as. This prompt is assembled based on a list of "facts" that the guild may configure using `/facts` commands like `/facts add`. 

An example of facts:
```
"You love wrestling, especially the attitude era."
"You are a fan of Star Trek."
"Your favorite food is german food. Especially sausages."
"You are an expert on EverQuest."
```

The above facts are the default for new guilds but can be managed by the guild through `/facts` commands.

# Setup
> This repo is currently designed to use **GPT-4**, as GPT-4 adheres to system prompts much much better than GPT-3.5. However, you might not immediately be granted access to GPT-4. If this is the case you can set the environment variable `OpenAiOptions__Model=gpt-3.5-turbo` or any other available model value. You may need to re-test the default prompts after making this change. Set this environment variable in the `docker-compose.yml` file at the root of the repo.

## Included Third-Party Libraries
- [Discord.NET](https://github.com/discord-net/Discord.Net)
    - For Discord API and Socket interaction
- [Polly](https://github.com/App-vNext/Polly)
    - For managing retries with the HTTP Client for Open AI API
- [LiteDB](https://github.com/mbdavid/LiteDB)
    - Very lightweight database for storing per-guild configuration
    - By default this will be created in 2 places:
        - For Docker, in a docker volume called 'litedb' on the host machine
        - For local development on Windows, in a folder inside ProgramData/LiteDB
        - Location is driven by the configuration key `DataServiceOptions__DatabasePath`

## Prerequisites
1. A [Docker](https://www.docker.com/) installation on your dev machine.
2. A [Discord Bot Token](https://discord.com/developers/docs/getting-started)
3. An [Open AI API Token](https://platform.openai.com/) with GPT-4 access

## Setup Steps
1. Clone the latest version of this repo
2. At the root, create a `.env` file with the following contents:
    ```bash
    BASE_IMAGE_NAME=your-base-image # e.g. myrepo/chatgptbot
    VERSION=1.0.0 # used as the docker image's tag
    DISCORD_TOKEN=your-discord-token
    OPENAI_API_KEY=your-openai-api-token
    SALT=your-custom-salt # this can be any random string, used to disguise the int ids for your underlying data
    ```
3. In `/Daemon` create a `local.settings.json` file with the following template:
    ```json
    {
        "Secrets": {
            "DiscordToken": "your-discord-token",
            "OpenAiApiKey": "your-open-ai-api-key",
            "Salt": "your-custom-salt"
        }
    }
    ```
    > The above template includes required values, but you can also modify other options. Check the classes located in `/Options` for other configurable settings. When debugging locally, values are read from `local.settings.json`. When running in docker, values are read from environment variables.
4. From a terminal at the root of the project, run docker compose with `docker compose up -d`
5. If setup was successful you should now have a container named `gptbot` running in docker. 

# Architecture Notes

The main source code is located in `/Daemon`.

Modifying the existing code is easy thanks to the simple project structure:
- /Accessors
    - Classes for accessing external services / data stores, including the OpenAI API and LiteDB database.
- /Builders
    - Builder classes for easily constructing prompts and ChatGPT messages.
- /Modules
    - [Discord.NET](https://github.com/discord-net/Discord.Net) Interaction Modules for /commands
- /Orchestrators
    - Top level orchestrators that manage the bulk of the code execution.
