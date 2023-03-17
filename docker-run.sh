for arg in "$@"
do
    if [ "$arg" == "--rm" ]
    then
        RM_FLAG=1
    fi
done

if [ "$RM_FLAG" == "1" ]
then
    DOCKER_RUN_ARGS="--rm"
else
    DOCKER_RUN_ARGS=""
fi

VERSION=$(cat ./version)

docker run \
$DOCKER_RM_ARG \
--detach \
--volume alexgpt:/usr/share/LiteDB \
-e TimedHostOptions__TimedHostTimeSpan="00:30:00" \
-e TimedHostOptions__ChanceOutOf100=50 \
-e TimedHostOptions__SleepStartTimeSpan="21:00:00" \
-e TimedHostOptions__SleepEndTimeSpan="09:00:00" \
--name alexgpt \
jakelunn/alexgpt:$VERSION