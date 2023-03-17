VERSION=$(cat ./version)

DOCKER_TAG_BASE=jakelunn/alexgpt
DOCKER_TAG=$DOCKER_TAG_BASE:$VERSION

docker build . -t $DOCKER_TAG