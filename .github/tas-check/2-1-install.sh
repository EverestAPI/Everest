#!/bin/bash
# Installs Everest from the branch to test, CelesteTAS and the mod that is going to be TASed.
# Parameters: URL of the TAS files, URL of the mod bundle

set -xeo pipefail

docker build \
	--build-arg "MAIN_BUILD_URL=`cat /tmp/everest-pr-tas-check/download-link.txt`" \
	--build-arg "TAS_FILES_URL=$1" \
	--build-arg "BUNDLE_DOWNLOAD_URL=$2" \
	-t celeste .
