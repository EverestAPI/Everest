#!/bin/bash
if TAG=`git describe --exact-match --tags 2>/dev/null`; then
  VERSION=${TAG#"v"}
else
  VERSION="0.0.${TRAVIS_BUILD_NUMBER}-travis"
fi
perl -0777 -pi -e 's/public readonly static string VersionString = ".*";/public readonly static string VersionString = "'${VERSION}'";/gm' ./Celeste.Mod.mm/Mod/Everest/Everest.cs
