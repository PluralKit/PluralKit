#!/bin/sh
docker-compose -f "$(dirname $0)/../docker-compose.yml" exec -T -u postgres db pg_dump postgres

