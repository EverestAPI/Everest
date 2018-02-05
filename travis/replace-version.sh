#!/bin/bash
if ! TAG=`git describe --exact-match --tags 2>/dev/null`; then
  echo "This commit is not tagged - not replacing version"
  exit 0
fi
VERSION=${TAG#"v"}
perl -0777 -pi -e 's/public readonly static string VersionString = ".*";/public readonly static string VersionString = "'${VERSION}'";/gm' ./Celeste.Mod.mm/Mod/Everest/Everest.cs
