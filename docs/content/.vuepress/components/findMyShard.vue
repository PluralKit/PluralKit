<template>
    <details>
        <summary><b>Find my shard</b></summary>
        <br>
        Enter a server ID or a message link to find the shard currently assigned to your server:
        <br>
        <input class="input" type="text" v-model="message" />
        <br><br>
        <span>{{ msg }}</span>
        <div v-if="valid">
            <h3>Your shard is: Shard {{ foundShard.id }}</h3>
            <br>
            <span>Status: <b>{{ foundShard.status }}</b></span><br>
            <span>Latency: {{ foundShard.ping }}ms</span><br>
            <span>Last connection: {{ new Date(foundShard.last_connection).toGMTString().match(/([0-9][0-9]:[0-9][0-9]:[0-9][0-9])/)[0] }}</span><br>
            <span>Last heartbeat: {{ new Date(foundShard.last_heartbeat).toGMTString().match(/([0-9][0-9]:[0-9][0-9]:[0-9][0-9])/)[0] }}</span><br>
        </div>
    </details>
</template>

<script>
// javascript wants everything to be BigInts
const getShardID = (guild_id, num_shards) => (BigInt(guild_id) >> BigInt(22)) % BigInt(num_shards);

export default {
    props: [ "shards" ],
    data: () => ({ show: false, message: "", valid: false, foundShard: {} }),
    computed: {
        msg() {
            if (this.message == "") {
                this.valid = false;
                return "";
            };
            var match = this.message.match(/https:\/\/[\w+]?discord[app]?.com\/channels\/(\d+)\/\d+\/\d+/);
            if (match != null) {
                this.foundShard = this.shards[getShardID(match[1], this.shards.length)];
                this.valid = true;
            }
            try {
                var shard = getShardID(this.message, this.shards.length);
                this.valid = true;
                this.foundShard = this.shards[shard];
            } catch(e) {
                return "Invalid shard ID";
            }
        },
    },
}
</script>

<style>
.input {
    margin-top: 10px;
    border: 1px solid #000;
}
</style>