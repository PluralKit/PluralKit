#!/bin/sh
docker-compose -f "$(dirname $0)/../docker-compose.yml" exec -u postgres db pg_dump postgres

