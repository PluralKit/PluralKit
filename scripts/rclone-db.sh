#!/bin/sh

# Usage: rclone-db.sh <remote>:<path>
# eg. rclone-db.sh b2:pluralkit

FILENAME=pluralkit-$(date -u +"%Y-%m-%dT%H:%M:%S").sql.gz
$(dirname $0)/dump-db.sh | gzip | rclone rcat $1/$FILENAME
