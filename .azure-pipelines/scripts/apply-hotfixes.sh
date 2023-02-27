#!/usr/bin/env bash

BRANCH_NAME=${BRANCH_NAME:-$(git rev-parse --abbrev-ref HEAD)}
COMMIT_BUFFER=${COMMIT_BUFFER:-1 month}

echo "Checking out mature changes..."

git fetch origin dev
git branch dev "origin/dev"
git checkout "dev@{$COMMIT_BUFFER ago}"

echo "Cherry-picking hotfixes..."
for commit in $(git log --since="$COMMIT_BUFFER ago" --grep="^hotfix:" --pretty=format:"%H" dev); do

    hotfix=$(git show --pretty=format:"%B" --no-patch "$commit" | grep "^hotfix:" --max-count=1)
    target=${hotfix#hotfix: }

    # Check whether the target commit is included in the release
    git rev-list --until="$COMMIT_BUFFER ago" HEAD | grep --quiet "^$target" || continue

    echo "Found hotfix commit:"
    git show --no-patch "$commit"

    # Resolve merge parents if possible
    case $(git rev-list --no-walk --count "$commit") in
        1);;
        2)
            cherry_pick_args="-m 2"
            ;;
        *)
            echo "Unable to cherry-pick a three-way merge commit. Waiting for manual validation."
            echo "##vso[task.setVariable variable=waitForManualValidation;isOutput=true]true"
            exit
    esac

    # Check if the patch has already been applied
    git cherry "$commit" && continue

    if ! git cherry-pick "$cherry_pick_args" "$commit" --allow-empty -x; then
        echo "There is a merge conflict. Waiting for manual validation."
        echo "##vso[task.setVariable variable=waitForManualValidation;isOutput=true]true"
        exit
    fi
done
