#!/bin/sh

notify () {
    curl $MGMT/notify -d "$1"
}

curl $MGMT/config > pluralkit.conf

notify "Cluster $NOMAD_ALLOC_INDEX starting"

export PluralKit__Bot__Cluster__NodeName="pluralkit-$NOMAD_ALLOC_INDEX"

dotnet bin/PluralKit.Bot.dll

notify "Cluster $NOMAD_ALLOC_INDEX exited with code $?"
