#!/bin/sh

# Parameters:
# $@:   value
output_version() {
    if [ "$TESTING" = 1 ]; then
        echo "$@"
    else
        VERSION="$*"
        echo "##vso[task.setVariable variable=version;isOutput=true]$VERSION"
        echo "##vso[task.setVariable variable=shortVersion;isOutput=true]${VERSION%%+*}"
    fi
}

main() {
    echo "Computing version number..."

    HOST=${HOST:-git}
    COMMIT_HASH=$(git rev-parse --short "${COMMIT_HASH:-HEAD}")
    BRANCH_NAME=$(git rev-parse --abbrev-ref "${BRANCH_NAME:-HEAD}")
    OFFSET=${OFFSET:-0}
    BACKDATE=${BACKDATE:-0 days}

    # Look for a tag on the current commit for the appropriate branch
    TAG_VERSION="$(git tag --points-at HEAD | grep "^$BRANCH_NAME-" --max-count=1)"
    if [ -n "$version" ]; then
        TAG_VERSION="${version#"$BRANCH_NAME"-}"
        output_version "$TAG_VERSION+$HOST.$COMMIT_HASH"
        exit
    fi


    MAJOR=1
    MINOR=$((OFFSET + $(date -d "$BACKDATE ago" +'%y%m')))
    PATCH=$(date -d "$BACKDATE ago" +'%d')
    VERSION="$MAJOR.$MINOR.$PATCH"

    echo "Looking for existing tags with prefix: $BRANCH_NAME-$VERSION*"
    git tag --list "$BRANCH_NAME-$VERSION*"
    REVISION=$(git tag --list "$BRANCH_NAME-$VERSION*" | wc -l)
    METADATA="$HOST.$COMMIT_HASH"
    VERSION="$VERSION.$REVISION+$METADATA"

    output_version "$VERSION"
}

test() {
    export TESTING=1
    echo "Running tests..."
    OFFSET=2000 BRANCH_NAME=beta $0
    OFFSET=2000 BRANCH_NAME=stable BACKDATE="2 months" $0
}

case $1 in
    test)
        test
        ;;
    *)
        main
        ;;
esac