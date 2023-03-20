VERSION=$(yq '.version' ./_config.yml)

# Create a secrets folder and add a discord-token, openai-key, and salt file.
# Note - this folder is added to the .gitignore file so it will not be checked into source control.
# Alternatively, modify this script to pull these values from a different source.
DISCORD_TOKEN=$(yq '.secrets.DiscordToken' ./_config.yml)
OPENAI_KEY=$(yq '.version' ./_config.yml)
SALT=$(yq '.version' ./_config.yml)

if [ -z "$DISCORD_TOKEN" ] || [ -z "$OPENAI_KEY" ] || [ -z "$SALT" ]; then
    echo "DISCORD_TOKEN, OPENAI_KEY or SALT is missing. Please create a secrets folder and add a discord-token, openai-key, and salt file."
    exit 1
fi

docker stop alexgpt || true
docker rm alexgpt || true

docker run \
--rm \
--detach \
--volume alexgpt:/usr/share/LiteDB \
-e TimedHostOptions__TimedHostTimeSpan="00:30:00" \
-e TimedHostOptions__ChanceOutOf100=10 \
-e TimedHostOptions__SleepStartTimeSpan="21:00:00" \
-e TimedHostOptions__SleepEndTimeSpan="09:00:00" \
-e GlobalDiscordOptions__Token=$DISCORD_TOKEN \
-e OpenAiOptions__ApiKey=$OPENAI_KEY \
-e DataServiceOptions__Salt=$SALT \
--name alexgpt \
jakelunn/alexgpt:$VERSION