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
        TAS_URL="https://github.com/VampireFlower/CelesteTAS/archive/5c9e67096767e494f66f39b587478de354e90bcf.zip"
        TAS_PATH="CelesteTAS-5c9e67096767e494f66f39b587478de354e90bcf/0 - 100%.tas"
        ;;

    "StrawberryJam2021")
        TAS_URL="https://github.com/VampireFlower/StrawberryJamTAS/archive/7e10cceb0ca812461f2bc81ec994871f0c2749f3.zip"
        TAS_PATH="StrawberryJamTAS-7e10cceb0ca812461f2bc81ec994871f0c2749f3/0-SJ All Levels.tas"
        BUNDLE_DOWNLOAD="https://celestemodupdater.0x0a.de/pinned-mods/StrawberryJam2021-Bundle-aa7323a3.zip"
        ;;

    *)
        echo "Unknown TAS: $1"
        exit 1
esac

cd "`dirname "$0"`"
./1-get-build-url.sh "$2"
./2-1-install.sh "${TAS_URL}" "${BUNDLE_DOWNLOAD}"
./3-run.sh "${TAS_PATH}"
