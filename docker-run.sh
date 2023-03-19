VERSION=$(cat ./version)

# Create a secrets folder and add a discord-token, openai-key, and salt file.
# Note - this folder is added to the .gitignore file so it will not be checked into source control.
# Alternatively, modify this script to pull these values from a different source.
DISCORD_TOKEN=$(cat ./secrets/discord-token)
OPENAI_KEY=$(cat ./secrets/openai-key)
SALT=$(cat ./secrets/salt)

if [ -z "$DISCORD_TOKEN" ] || [ -z "$OPENAI_KEY" ] || [ -z "$SALT" ]; then
    echo "DISCORD_TOKEN, OPENAI_KEY or SALT is missing. Please create a secrets folder and add a discord-token, openai-key, and salt file."
    exit 1
fi

docker run \
--rm \
--detach \
--volume alexgpt:/usr/share/LiteDB \
-e TimedHostOptions__TimedHostTimeSpan="00:30:00" \
-e TimedHostOptions__ChanceOutOf100=30 \
-e TimedHostOptions__SleepStartTimeSpan="21:00:00" \
-e TimedHostOptions__SleepEndTimeSpan="09:00:00" \
-e GlobalDiscordOptions__Token=$DISCORD_TOKEN \
-e OpenAiOptions__ApiKey=$OPENAI_KEY \
-e DataServiceOptions__Salt=$SALT \
--name alexgpt \
jakelunn/alexgpt:$VERSION