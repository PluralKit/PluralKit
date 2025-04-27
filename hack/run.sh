#!/bin/sh

echo "nameserver 127.0.0.1" > /etc/resolv.conf

/hack/coredns -conf /hack/Corefile &

dotnet bin/PluralKit.Bot.dll
