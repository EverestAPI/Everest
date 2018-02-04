#!/bin/bash

# Copyright (c) 2014 Terry Burton
#
# https://github.com/terryburton/travis-github-release
#
# Permission is hereby granted, free of charge, to any
# person obtaining a copy of this software and associated
# documentation files (the "Software"), to deal in the
# Software without restriction, including without
# limitation the rights to use, copy, modify, merge,
# publish, distribute, sublicense, and/or sell copies of
# the Software, and to permit persons to whom the Software
# is furnished to do so, subject to the following
# conditions:
#
# The above copyright notice and this permission notice
# shall be included in all copies or substantial portions
# of the Software.
#
# THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY
# KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO
# THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A
# PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
# THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
# DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF
# CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
# CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
# IN THE SOFTWARE.

# This script provides a simple continuous deployment
# solution that allows Travis CI to publish a new GitHub 
# release and upload assets to it whenever a tag is pushed:
# git tag; git push --tags
#
# It was created as a temporary solution whilst waiting for
# Travis DPL to support GitHub, which it now does:
#
# http://docs.travis-ci.com/user/deployment/releases/
#
# Place this script somewhere in your project repository (perhaps by forking
# the github-travis-release repo and adding your fork as a git submodule) then
# put something like this to your .travis.yml:
#
# after_success: .travis/github-release.sh "$TRAVIS_REPO_SLUG" "`head -1 src/VERSION`" build/release/*
#
# The first argument is your repository in the format
# "username/repository", which Travis provides in the
# TRAVIS_REPO_SLUG environment variable.
#
# The second argument is the release version which as a
# sanity check should match the tag that you are releasing.
# You could pass "`git describe`" to satisfy this check.
#
# The remaining arguments are a list of asset files that you
# want to publish along with the release.
#
# The script requires that you create a GitHub OAuth access
# token to facilitate the upload:
#
# https://help.github.com/articles/creating-an-access-token-for-command-line-use
#
# You must pass this securely in the GITHUBTOKEN environment
# variable:
#
# http://docs.travis-ci.com/user/encryption-keys/
#
# For testing purposes you can create a local convenience
# file in the script directory called GITHUBTOKEN that sets
# the GITHUBTOKEN environment variable. If you do so you MUST
# ensure that this doesn't get pushed to your repository,
# perhaps by adding it to a .gitignore file.
#
# Should you get stuck then look at a working example. This
# code is being used by Barcode Writer in Pure PostScript
# for automated deployment:
#
# https://github.com/bwipp/postscriptbarcode

set -e

REPO=$1 && shift
RELEASE=$1 && shift
RELEASEFILES=$@

if ! TAG=`git describe --exact-match --tags 2>/dev/null`; then
  echo "This commit is not a tag so not creating a release"
  exit 0
fi

if [ "$TRAVIS" = "true" ] && [ -z "$TRAVIS_TAG" ]; then
  echo "This build is not for the tag so not creating a release"
  exit 0
fi

if [ "$TRAVIS" = "true" ] && [ "$TRAVIS_TAG" != "$RELEASE" ]; then
  echo "Error: TRAVIS_TAG ($TRAVIS_TAG) does not match the indicated release ($RELEASE)"
  exit 1
fi

if [ "$TAG" != "$RELEASE" ]; then
  echo "Error: The tag ($TAG) does not match the indicated release ($RELEASE)"
  exit 1
fi

if [[ -z "$RELEASEFILES" ]]; then
  echo "Error: No release files provided"
  exit 1
fi

SCRIPTDIR=`dirname $0`
[ -e "$SCRIPTDIR/GITHUBTOKEN" ] && . "$SCRIPTDIR/GITHUBTOKEN"
if [[ -z "$GITHUBTOKEN" ]]; then
  echo "Error: GITHUBTOKEN is not set"
  exit 1
fi

echo "Checking if GitHub release for $RELEASE exists already"
RESULT=`curl -s -w "\n%{http_code}\n"     \
  -H "Authorization: token $GITHUBTOKEN"  \
  "https://api.github.com/repos/$REPO/releases/tags/$RELEASE"`
if [ "`echo "$RESULT" | tail -1`" = "404" ]; then
  echo "Creating GitHub release for $RELEASE"
  RELEASENEW=true

  echo -n "Create draft release... "
JSON=$(cat <<EOF
{
  "tag_name":         "$TAG",
  "target_commitish": "master",
  "name":             "$TAG",
  "draft":            true,
  "prerelease":       false
}
EOF
)
  RESULT=`curl -s -w "\n%{http_code}\n"     \
    -H "Authorization: token $GITHUBTOKEN"  \
    -d "$JSON"                              \
    "https://api.github.com/repos/$REPO/releases"`
  if [ "`echo "$RESULT" | tail -1`" != "201" ]; then
    echo FAILED
    echo "$RESULT" 
    exit 1
  fi 
  RELEASEID=`echo "$RESULT" | sed -ne 's/^  "id": \(.*\),$/\1/p'`
  if [[ -z "$RELEASEID" ]]; then
    echo FAILED
    echo "$RESULT" 
    exit 1
  fi
  echo DONE
elif [ "`echo "$RESULT" | tail -1`" = "200" ]; then
  echo "GitHub release $RELEASE existing, adding assets."
  RELEASEID=`echo "$RESULT" | sed -ne 's/^  "id": \(.*\),$/\1/p'`
  echo "Release ID: \"$RELEASEID\""
else
  echo "Error: No release files provided"
  exit 1
fi

for FILE in $RELEASEFILES; do
  if [ ! -f $FILE ]; then
    echo "Warning: $FILE not a file"
    continue
  fi
  FILESIZE=`stat -c '%s' "$FILE"`
  FILENAME=`basename $FILE`
  echo -n "Uploading $FILENAME... "
  RESULT=`curl -s -w "\n%{http_code}\n"                   \
    -H "Authorization: token $GITHUBTOKEN"                \
    -H "Accept: application/vnd.github.manifold-preview"  \
    -H "Content-Type: application/octet-stream"           \
    --data-binary "@$FILE"                                \
    "https://uploads.github.com/repos/$REPO/releases/$RELEASEID/assets?name=$FILENAME"`
  if [ "`echo "$RESULT" | tail -1`" != "201" ]; then
    echo FAILED
    echo "$RESULT" 
    exit 1
  fi
  echo DONE
done 

if [ "$RELEASENEW" = true ]; then
  echo -n "Publishing release... "
JSON=$(cat <<EOF
{
  "draft": false
}
EOF
)
  RESULT=`curl -s -w "\n%{http_code}\n"     \
    -X PATCH                                \
    -H "Authorization: token $GITHUBTOKEN"  \
    -d "$JSON"                              \
    "https://api.github.com/repos/$REPO/releases/$RELEASEID"`
  if [ "`echo "$RESULT" | tail -1`" = "200" ]; then
    echo DONE
  else
    echo FAILED
    echo "$RESULT"
    exit 1
  fi
fi
