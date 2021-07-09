---
title: Status
description: PluralKit status page
permalink: /status
---

# Bot status

<noscript>Please enable JavaScript to view this page!</noscript>

<template>
    <ClientOnly>
        {{ shards.length }} shards ({{ shards.filter(x => x.status == "up").length }} up) <br>
        Average latency: {{ pingAverage }}ms
        <br><br>
        More statistics available at <a href="https://stats.pluralkit.me">https://stats.pluralkit.me</a>
        <br><br>
        <findMyShard :shards="shards"/>
    </ClientOnly>
</template>

## Shard status

<template>
    <ClientOnly>
        <span>{{ message }}</span>
        <div v-for="shard in shards">
            <shard :shard="shard" />
        </div>
    </ClientOnly>
</template>

<script>
export default {
    data: () => ({ message: "Loading...", shards: [], pingAverage: 0, }),
    async mounted() {
        const shards = (await (await fetch("https://api.pluralkit.me/v1/meta")).json()).shards.sort((x, y) => (x.id > y.id) ? 1 : -1);
        let pings = 0;
        this.shards = shards.map(shard => {
            shard.ping = Math.trunc(shard.ping * 1000);
            pings += shard.ping;
            return shard;
        });

        this.pingAverage = Math.trunc(pings / this.shards.length);

        this.message = "";
    }
}
</script>

<style global>
* {
    margin: 0px;
    border: none;
}

</style>