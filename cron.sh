#!/bin/sh
opts=$1
rclone_db(){
FILENAME=pluralkit-$(date -u +"%Y-%m-%dT%H:%M:%S").sql.gz
dump_db | gzip | rclone rcat $2/$FILENAME
}

dump_db(){
docker-compose -f "$(dirname $0)/../docker-compose.yml" exec -T -u postgres db pg_dump postgres
}


usage(){
	docker-compose -f "$(dirname $0)/../docker-compose.yml" exec -T -u postgres db pg_dump postgres
}

case $opts in
rclone_db) rclone_db;;
dump_db) dump_db ;;
*) usage ;;
esac
