FROM ubuntu

ADD Celeste.tar.gz /home/ubuntu/

ARG MAIN_BUILD_URL TAS_FILES_URL MODPACK_NAME

RUN apt-get update \
    && apt-get upgrade -y \
    && apt-get install -y curl unzip libicu-dev \
    && cd /home/ubuntu \
    && curl --fail -Lo everest.zip "${MAIN_BUILD_URL}" \
    && unzip everest.zip \
    && rm -v everest.zip \
    && mv -fv main/* celeste/ \
    && rm -rfv main \
    && cd celeste \
    && chmod -v u+x MiniInstaller-linux \
    && ./MiniInstaller-linux headless \
    && cd .. \
    && curl --fail -Lo t.zip "https://celestemodupdater.0x0a.de/pinned-mods/TAS-Files-${TAS_TO_RUN}.zip" \
    && unzip t.zip \
    && rm -v t.zip \
    && cd celeste/Mods \
    && curl --fail -Lo CelesteTAS.zip "https://celestemodupdater.0x0a.de/pinned-mods/CelesteTAS.zip" \
    && curl --fail -Lo t.zip "https://celestemodupdater.0x0a.de/pinned-mods/${TAS_TO_RUN}-Bundle.zip" \
    && unzip t.zip \
    && rm -v t.zip

ENV LD_LIBRARY_PATH="/home/ubuntu/celeste/lib64-linux"
WORKDIR /home/ubuntu/celeste
ENTRYPOINT ["/home/ubuntu/celeste/Celeste"]