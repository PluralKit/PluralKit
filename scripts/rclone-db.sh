#!/bin/sh

# Usage: rclone-db.sh <remote>:<path>
# eg. rclone-db.sh b2:pluralkit

FILENAME=pluralkit-$(date -u +"%Y-%m-%dT%H:%M:%S").sql.gz

echo Dumping database to /tmp/$FILENAME...
$(dirname $0)/dump-db.sh | gzip > /tmp/$FILENAME

echo Transferring to remote $1...
rclone -P copy /tmp/$FILENAME $1

echo Cleaning up...
rm /tmp/$FILENAME