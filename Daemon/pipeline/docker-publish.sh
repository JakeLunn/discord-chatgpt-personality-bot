BASE_NAME=$(yq '.baseName' ./_config.yml)
VERSION=$(yq '.version' ./_config.yml)

docker tag $BASE_NAME:$VERSION $BASE_NAME
docker push $BASE_NAME
docker push $DOCKER_TAG_BASE:$VERSION 