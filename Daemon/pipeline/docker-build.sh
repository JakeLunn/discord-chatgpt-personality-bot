BASE_NAME=$(yq '.baseName' ./_config.yml)
VERSION=$(yq '.version' ./_config.yml)

DOCKER_TAG=$BASE_NAME:$VERSION

docker build . -t $DOCKER_TAG