source .env

docker tag $BASE_IMAGE_NAME $BASE_IMAGE_NAME:$VERSION
docker push $BASE_IMAGE_NAME
docker push $BASE_IMAGE_NAME:$VERSION