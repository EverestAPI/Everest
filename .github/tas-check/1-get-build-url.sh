#!/bin/bash
# Waits for Azure to be finished building, and writes the download link to /tmp/everest-pr-tas-check/download-link.txt.
# Parameter: commit SHA

set -xeo pipefail

get_build_id() {
	curl 'https://dev.azure.com/EverestAPI/Everest/_apis/build/builds?definitions=3&statusFilter=completed' \
		| jq -r ".value | map(select(.sourceVersion == \"${1}\")) | .[].id"
}

BUILD_ID=`get_build_id "$1"`
while [ "${BUILD_ID}" == "" ]; do
	sleep 60
	BUILD_ID=`get_build_id "$1"`
done

mkdir -p /tmp/everest-pr-tas-check
echo -n "https://dev.azure.com/EverestAPI/Everest/_apis/build/builds/${BUILD_ID}/artifacts?artifactName=main&api-version=5.0&%24format=zip" > /tmp/everest-pr-tas-check/download-link.txt