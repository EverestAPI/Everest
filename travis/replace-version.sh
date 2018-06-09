#!/bin/bash
if TAG=`git describe --exact-match --tags 2>/dev/null`; then
  VERSION=${TAG#"v"}
else
  VERSION="1.${TRAVIS_BUILD_NUMBER}.0-travis-$(git rev-parse --short=5 HEAD)"
fi
perl -0777 -pi -e 's/public readonly static string VersionString = ".*";/public readonly static string VersionString = "'${VERSION}'";/gm' ./Celeste.Mod.mm/Mod/Everest/Everest.cs
