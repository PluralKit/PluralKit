#!/bin/sh

# Runs a local database in the background listening on port 5432, deleting itself once stopped
# Requires Docker. May need sudo if your user isn't in the `docker` group.  
docker run --rm --detach --publish 5432:5432 -e POSTGRES_HOST_AUTH_METHOD=trust postgres:alpine