#!/bin/sh

rm ../.version || true
touch ../.version
git rev-parse HEAD >> ../.version
git show --no-patch --format=%at $(git rev-parse HEAD) >> ../.version
(git diff-index --quiet HEAD -- && echo 1) >> ../.version || true
