#!/bin/sh

set -euo pipefail

notify_discord() {
  todo
}

# CI_PREV_COMMIT
# GH_BRANCH

files_changed=$(git diff --name-only $CI_PREV_COMMIT)

if [ ! -z "$(echo $files_changed | grep -E '.cs$')" ]; then
  dotnet_format
fi

if [ ! -z "$(echo $files_changed | grep -E '.rs$')" ]; then
  rustfmt
fi

###

if PluralKit.Bot changed build bot
if PluralKit.Core changed build bot api
idk this should just be python
