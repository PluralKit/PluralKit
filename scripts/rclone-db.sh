#!/bin/sh

# Usage: rclone-db.sh <remote>:<path>
# eg. rclone-db.sh b2:pluralkit

FILENAME=pluralkit-$(date +"%Y-%m-%dT%H:%M:%S").sql
$(dirname $0)/dump-db.sh | rclone rcat $1/$FILENAME

