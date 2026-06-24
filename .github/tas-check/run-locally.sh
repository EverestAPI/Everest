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
        TAS_URL="https://github.com/VampireFlower/CelesteTAS/archive/d34bd24434888ea15ef36d91522a803749cda96d.zip"
        TAS_PATH="CelesteTAS-d34bd24434888ea15ef36d91522a803749cda96d/0 - 100%.tas"
        ;;

    "StrawberryJam2021")
        TAS_URL="https://github.com/VampireFlower/StrawberryJamTAS/archive/9e019be8c48d2784089315eede0201ed058f82a4.zip"
        TAS_PATH="StrawberryJamTAS-9e019be8c48d2784089315eede0201ed058f82a4/0-SJ All Levels.tas"
        BUNDLE_DOWNLOAD="https://celestemodupdater.0x0a.de/pinned-mods/StrawberryJam2021-Bundle-8b6d87e2.zip"
        ;;

    *)
        echo "Unknown TAS: $1"
        exit 1
esac

cd "`dirname "$0"`"
./1-get-build-url.sh "$2"
./2-1-install.sh "${TAS_URL}" "${BUNDLE_DOWNLOAD}"
./3-run.sh "${TAS_PATH}"
