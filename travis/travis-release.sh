#!/bin/bash

chmod a+x ./travis/github-release.sh
chmod a+x ./travis/html-gen.sh

# Taken from https://gist.github.com/chrismdp/6c6b6c825b07f680e710
# Adapted for our S3 digitalocean target.
function putS3
{
  path=$1
  file=$2
  aws_path=$3
  bucket='lollyde'
  date=$(date +"%a, %d %b %Y %T %z")
  size=$(wc -c < "$path/$file" | tr -d '\n')
  acl="x-amz-acl:public-read"
  content_type=$4
  string="PUT\n\n$content_type\n$date\n$acl\n/$bucket$aws_path$file"
  signature=$(echo -en "${string}" | openssl sha1 -hmac "${S3SECRET}" -binary | base64)
  curl -X PUT -T "$path/$file" \
    -H "Host: $bucket.ams3.digitaloceanspaces.com" \
    -H "Date: $date" \
    -H "Content-Type: $content_type" \
    -H "$acl" \
    -H "Authorization: AWS ${S3KEY}:$signature" \
	-H "Content-Length: $size" \
    "https://$bucket.ams3.digitaloceanspaces.com$aws_path$file"
}

if [ "$TRAVIS_PULL_REQUEST" = "false" ] ; then
  SUFFIX=""
  if [ "$TRAVIS_BRANCH" != "master" ] ; then
    SUFFIX="-$TRAVIS_BRANCH"
  fi
  
  ROOT="$(pwd)"
  ZIP="build-${TRAVIS_BUILD_NUMBER}${SUFFIX}.zip"
  
  echo "Creating build .zip"
  pushd Celeste.Mod.mm/Artifact
  zip "$ROOT/$ZIP" *
  popd
  pushd MiniInstaller/Artifact
  zip "$ROOT/$ZIP" *
  popd
  chmod a+x mod.sh
  zip "$ROOT/$ZIP" mod.bat mod.sh
  
  echo "Get latest builds_index.txt"
  wget -O ./travis/builds_index.txt https://lollyde.ams3.digitaloceanspaces.com/everest-travis/builds_index.txt
  
  echo "Update builds_index.txt"
  printf "/lollyde/everest-travis/$ZIP $ZIP\n" >> ./travis/builds_index.txt
  
  echo "Create updated index.html"
  ./travis/html-gen.sh
  
  echo "Pushing build to S3"
  putS3 "$ROOT" "$ZIP" "/everest-travis/" 'application/x-compressed-zip'
  
  echo "Pushing index.html to S3"
  putS3 "$ROOT/travis" "index.html" "/" 'text/html'
  
  echo "Pushing builds_index.txt to S3"
  putS3 "$ROOT/travis/" "builds_index.txt" "/everest-travis/" 'text/plain'
  
  
  if [ "$TRAVIS_BRANCH" = "$TRAVIS_TAG" ] ; then
	echo "Pushing release"
	./travis/github-release.sh "$TRAVIS_REPO_SLUG" "$TRAVIS_TAG" "$ROOT/$ZIP"
  fi
  
  echo "Done."
  
  
fi
