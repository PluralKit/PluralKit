<script lang="ts">
    import { Container } from 'sveltestrap';
    import ShardItem from '../lib/shard.svelte';

    let message = "Loading...";
    let shards = [];
    let pingAverage = "";
    let currentCommitMsg = "";

    let foundShard = {
        id: 1,
        status: 1,
        ping:"",
        last_connection:0,
        last_heartbeat:0.
    };
    foundShard = null;

    let findShardInput = "";
    let valid = false;

    const get = async () => {
        const pkdata = await fetch("https://api.pluralkit.me/private/meta").then(x => x.json());
            shards = pkdata.shards.sort((x, y) => (x.id > y.id) ? 1 : -1);
            let pings = 0;
            shards = shards.map(shard => {
                    pings += shard.ping;
                    shard.last_connection = new Date(Number(shard.last_connection) * 1000).toUTCString().match(/([0-9][0-9]:[0-9][0-9]:[0-9][0-9])/)?.shift()
                    shard.last_heartbeat = new Date(Number(shard.last_heartbeat) * 1000).toUTCString().match(/([0-9][0-9]:[0-9][0-9]:[0-9][0-9])/)?.shift()
                    return shard;
            });
    
            pingAverage = Math.trunc(pings / shards.length).toString();
    
            currentCommitMsg = `Current Git commit: <a href="https://github.com/xSke/PluralKit/commit/${pkdata.version}">${pkdata.version}</a>`;
    
            message = "";
    };

    get();
    setTimeout(get, 30 * 1000);

    // javascript wants everything to be BigInts
    const getShardID = (guild_id: string, num_shards: number) => guild_id == "" ? -1 : (BigInt(guild_id) >> BigInt(22)) % BigInt(num_shards);
    
    let shardInfoMsg = "";

    let shardInfoHandler = (_: Event) => {
        if (findShardInput == "" || !findShardInput) {
            valid = false;
            foundShard = null;
            shardInfoMsg = "";
            return;
        };
        var match = findShardInput.match(/https:\/\/[\w+]?discord[app]?.com\/channels\/(\d+)\/\d+\/\d+/);
        if (match != null) {
            console.log("match", match)
            foundShard = shards[Number(getShardID(match[1], shards.length))];
            valid = true;
            shardInfoMsg = "";
            return;
        }
        try {
            var shard = getShardID(findShardInput, shards.length);
            if (shard == -1) {
                valid = false;
                foundShard == null;
                shardInfoMsg = "Invalid server ID";
                return;
            }
            foundShard = shards[Number(shard)];
            valid = true;
            shardInfoMsg = "";
        } catch(e) {
            valid = false;
            shardInfoMsg = "Invalid server ID";
        }
    };
    
</script>

<Container>
    <h1>Bot status</h1>
    <span>{@html currentCommitMsg}</span>
    <br>
    <noscript>Please enable JavaScript to view this page!</noscript>

    { shards.length } shards ({ shards.filter(x => x.status == "up").length } up) <br>
    Average latency: { pingAverage }ms
    <br><br>
    All times in UTC. More statistics available at <a href="https://stats.pluralkit.me">https://stats.pluralkit.me</a>
    <br><br>
    <details>
        <summary><b>Find my shard</b></summary>
        <br>
        Enter a server ID or a message link to find the shard currently assigned to your server:
        <br>
        <input bind:value={findShardInput} on:input={shardInfoHandler} />
        <br><br>
        <span>{ shardInfoMsg }</span>
        {#if valid}
            <h3>Your shard is: Shard { foundShard.id }</h3>
            <br>
            <span>Status: <b>{ foundShard.status }</b></span><br>
            <span>Latency: { foundShard.ping }ms</span><br>
			<span>Disconnection count: { foundShard.disconnection_count }</span><br>
            <span>Last connection: { foundShard.last_connection }</span><br>
            <span>Last heartbeat: { foundShard.last_heartbeat }</span><br>
        {/if}
    </details>
    <br><br>
    <h2>Shard status</h2>

    <span>{ message }</span>

    {#each shards as shard}
        <ShardItem shard={shard} />
    {/each}
</Container>