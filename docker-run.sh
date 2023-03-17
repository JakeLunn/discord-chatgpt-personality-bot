if [ -z "$1" ]; then
    echo "Usage: $0 [version]"
    exit 1
fi

docker run \
--rm \
--detach \
--volume alexgpt:/usr/share/LiteDB \
-e TimedHostOptions__TimedHostTimeSpan="00:30:00" \
-e TimedHostOptions__ChanceOutOf100=50 \
-e TimedHostOptions__SleepStartTimeSpan="21:00:00" \
-e TimedHostOptions__SleepEndTimeSpan="09:00:00" \
--name alexgpt \
jakelunn/alexgpt:$1