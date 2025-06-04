#!/bin/bash

set -xeo pipefail

case "$1" in
	"Celeste")
		SYNC_CHECK_FILE="CelesteTAS-master/0 - 100%.tas"
		;;

	"StrawberryJam2021")
		SYNC_CHECK_FILE="Mods/StrawberryJamTAS-main/0-SJ All Levels.tas"
		;;

	*)
		echo "Unknown TAS: $1"
		exit 1
		;;
esac


get_build_id() {
	curl 'https://dev.azure.com/EverestAPI/Everest/_apis/build/builds?definitions=3&resultFilter=succeeded&statusFilter=completed' \
		| jq -r ".value | map(select(.triggerInfo[\"pr.sourceSha\"] == \"${GITHUB_SHA}\")) | .[].id"
}

BUILD_ID=`get_build_id`
while [ "${BUILD_ID}" == "" ]; do
	sleep 60
	BUILD_ID=`get_build_id`
done

docker build \
	--build-arg "MAIN_BUILD_URL=https://dev.azure.com/EverestAPI/Everest/_apis/build/builds/${BUILD_ID}/artifacts?artifactName=main&api-version=5.0&%24format=zip" \
	--build-arg "TAS_TO_RUN=$1" \
	-t celeste .

mkdir -p checker-output
docker run \
	--volume "`pwd`/checker-output:/home/ubuntu/tas" \
	--rm \
	--name celeste celeste \
	--sync-check-file "/home/ubuntu/${SYNC_CHECK_FILE}" \
	--sync-check-result /home/ubuntu/tas/result.json

[ "`jq -r '.entries.[].status' checker-output/result.json`" == "success" ]