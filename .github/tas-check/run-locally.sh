#!/bin/bash
# Builds and runs the TAS locally, calling the same scripts as the pipeline in order to test them.
# Requires Docker and jq.

if [ "$1" == "" ] || [ "$2" == "" ]; then
    echo "Usage: ./run-locally.sh [Celeste|StrawberryJam2021] [commit SHA]"
    exit 1
fi

set -xeo pipefail

case "$1" in
    "Celeste")
        TAS_URL="https://github.com/VampireFlower/CelesteTAS/archive/a30f9dbc8b89039c4ecb9f21a60ae434a23a44e1.zip"
        TAS_PATH="CelesteTAS-a30f9dbc8b89039c4ecb9f21a60ae434a23a44e1/0 - 100%.tas"
        ;;

    "StrawberryJam2021")
        TAS_URL="https://github.com/VampireFlower/StrawberryJamTAS/archive/273058abe5deae5b1e2f1d166d242586d5f6ef0c.zip"
        TAS_PATH="StrawberryJamTAS-273058abe5deae5b1e2f1d166d242586d5f6ef0c/0-SJ All Levels.tas"
        BUNDLE_DOWNLOAD="https://celestemodupdater.0x0a.de/pinned-mods/StrawberryJam2021-Bundle-906ca6b1.zip"
        ;;

    *)
        echo "Unknown TAS: $1"
        exit 1
esac

cd "`dirname "$0"`"
./1-get-build-url.sh "$2"
./2-1-install.sh "${TAS_URL}" "${BUNDLE_DOWNLOAD}"
./3-run.sh "${TAS_PATH}"
