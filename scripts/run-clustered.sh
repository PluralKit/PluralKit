#!/bin/sh

notify () {
    curl http://172.17.0.1:8081/notify -d "$1"
}

curl http://172.17.0.1:8081/config > pluralkit.conf

notify "Cluster $NOMAD_ALLOC_INDEX starting"

export PluralKit__Bot__Cluster__NodeName="pluralkit-$NOMAD_ALLOC_INDEX"

dotnet bin/PluralKit.Bot.dll

notify "Cluster $NOMAD_ALLOC_INDEX exited with code $?"
