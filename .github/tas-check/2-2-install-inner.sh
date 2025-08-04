#!/bin/bash
# Installs Everest from the branch to test, CelesteTAS and the mod that is going to be TASed.
# Run from within the Docker image, where Celeste is installed at /home/ubuntu/celeste.

set -xeo pipefail

# download Everest
cd /home/ubuntu
curl --fail -Lo everest.zip "${MAIN_BUILD_URL}"
unzip everest.zip
rm -v everest.zip

# copy Everest files to Celeste install
mv -fv main/* celeste/
rm -rfv main

# install Everest in headless mode
cd celeste
chmod -v u+x MiniInstaller-linux
./MiniInstaller-linux headless

# download TAS files
cd ..
curl --fail -Lo t.zip "${TAS_FILES_URL}"
unzip t.zip
rm -v t.zip

# install CelesteTAS
cd celeste/Mods
curl --fail -Lo CelesteTAS.zip "https://github.com/EverestAPI/CelesteTAS-EverestInterop/releases/download/v3.46.0/CelesteTAS.zip"

# install the mod that is going to be TASed, downloaded as a bundle zip containing the mod zip
# and all of its dependencies (https://maddie480.ovh/celeste/bundle-download?id=${TAS_TO_RUN})
if ! [ "${BUNDLE_DOWNLOAD_URL}" == "" ]; then
    curl --fail -Lo t.zip "${BUNDLE_DOWNLOAD_URL}"
    unzip t.zip
    rm -v t.zip
fi
