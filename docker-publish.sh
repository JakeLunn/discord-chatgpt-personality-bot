if [ -z "$1" ]; then
    echo "Usage: $0 [version]"
    exit 1
fi

VERSION=$1

DOCKER_TAG_BASE=jakelunn/alexgpt
DOCKER_TAG=$DOCKER_TAG_BASE:$VERSION

docker build . -t $DOCKER_TAG
docker tag $DOCKER_TAG_BASE:$VERSION $DOCKER_TAG_BASE
docker push $DOCKER_TAG
docker push $DOCKER_TAG_BASE