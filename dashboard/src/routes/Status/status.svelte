<script lang="ts">
    import { Container, Row, Col, Card, CardHeader, CardTitle, CardBody, Input, Button } from 'sveltestrap';
    import FaInfoCircle from 'svelte-icons/fa/FaInfoCircle.svelte'
    import ShardItem from '../../components/status/Shard.svelte';

    import api from '../../api';

    let message = "Loading...";
    let shards = [];
    let clusters = {};
    let pingAverage = "";

    let foundShard = {
        id: 1,
        up: false,
        latency:"",
        disconnection_count:0,
        last_connection:0,
        last_heartbeat:0,
        heartbeat_minutes_ago:0
    };
    foundShard = null;

    let findShardInput = "";
    let valid = false;

    const get = async () => {
        const pkdata = await api().private.discord.shard_state.get();
        let data = pkdata.shards.sort((x, y) => (x.id > y.id) ? 1 : -1);
        let latencies = 0;
        data = data.map(shard => {
            latencies += shard.latency;
            shard.heartbeat_minutes_ago = heartbeatMinutesAgo(shard);
            shard.last_connection =  new Date(Number(shard.last_connection) * 1000).toUTCString().match(/([0-9][0-9]:[0-9][0-9]:[0-9][0-9])/)?.shift()
            shard.last_heartbeat = new Date(Number(shard.last_heartbeat) * 1000).toUTCString().match(/([0-9][0-9]:[0-9][0-9]:[0-9][0-9])/)?.shift()
            return shard;
        });

        if (data[0].cluster_id !== undefined) {
            let clusterData = {};
            data.forEach(shard => {
                if (clusterData[shard.cluster_id] === undefined) clusterData[shard.cluster_id] = [];
                clusterData[shard.cluster_id].push(shard);
            });
            clusters = clusterData;
        }

        Object.keys(clusters).map(c => clusters[c] = clusters[c].reverse());

        shards = data;
        pingAverage = Math.trunc(latencies / shards.length).toString();

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
        var match = findShardInput.match(/https:\/\/(?:[\w]*\.)?discord(?:app)?\.com\/channels\/(\d+)\/\d+\/\d+/);
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
    
    function heartbeatMinutesAgo(shard) {
		// difference in milliseconds
		const msDifference = Math.abs((Number(new Date(Number(shard.last_heartbeat) * 1000)) - Date.now()));
		// convert to minutes
		const minuteDifference = msDifference / (60 * 1000);
		
        return minuteDifference;
	}
</script>

<Container>
    <Row>
        <Col class="mx-auto" xs={12} lg={11} xl={10}>
            <Card class="mb-4">
                <CardHeader>
                    <CardTitle style="margin-top: 8px; outline: none;">
                        <div class="icon d-inline-block">
                            <FaInfoCircle />
                        </div>
                        Bot status
                    </CardTitle>
                </CardHeader>
                <CardBody>
                    <br>
                    <noscript>Please enable JavaScript to view this page!</noscript>

                    <Row>
                        <Col class="mb-2" xs={12} md={6} lg={4} >
                            <span>{ shards.length } shards ({ shards.filter(x => x.up).length } up)</span>
                        </Col>
                        <Col class="mb-2" xs={12} md={6} lg={4}>
                            <span>Average latency: { pingAverage }ms</span>
                        </Col>
                        <Col class="mb-2" xs={12} md={6} lg={4}>
                            <span>More statistics available at <a href="https://stats.pluralkit.me">https://stats.pluralkit.me</a></span>
                        </Col>
                    </Row>
                    <hr/>
                    <h3 style="font-size: 1.2rem;">Find my shard</h3>
                        <p>Enter a server ID or a message link to find the shard currently assigned to your server</p>
                        <label for="shardInput">Server ID or message link</label>
                        <input id="shardInput" class="form form-control" bind:value={findShardInput} on:input={shardInfoHandler} />
                        {#if shardInfoMsg || foundShard}
                        <Card class="mt-4">
                            <CardHeader>
                                <CardTitle>
                                    {#if shardInfoMsg}
                                        {shardInfoMsg}
                                    {/if}
                                    {#if foundShard}
                                        Your shard is: Shard { foundShard.shard_id }
                                    {/if}
                                </CardTitle>
                            </CardHeader>

                        {#if valid}
                            <CardBody>
                            <span>Status: <b>{ foundShard.up ? "up" : "down"}</b></span><br>
                            <span>Latency: { foundShard.latency }ms</span><br>
                            <span>Disconnection count: { foundShard.disconnection_count }</span><br>
                            <span>Last connection: { foundShard.last_connection } UTC</span><br>
                            <span>Last heartbeat: { foundShard.last_heartbeat } UTC</span><br>
                            </CardBody>
                        {/if}
                        </Card>
                        {/if}
                </CardBody>
            </Card>
        </Col>
    </Row>
    {#if Object.keys(clusters).length == 0 && shards.length > 0}
        <Row>
            <Col class="mx-auto" xs={12} lg={11} xl={10}>
                <Card class="mb-4">
                    <CardBody>
                        <span>{ message }</span>
                        {#each shards as shard}
                            <ShardItem shard={shard} />
                        {/each}
                    </CardBody>
                </Card>
            </Col>
        </Row>
    {/if}
    <Row>
        <Col class="mx-auto" xs={12} lg={11} xl={10}>
        <div class="cluster-grid">
        {#each Object.keys(clusters) as key}
            <div class="cluster-card">
                <span class="cluster-text">Cluster {key} &nbsp</span>
            </div>
            <div class="cluster-shards">
                {#each clusters[key] as shard}
                <ShardItem shard={shard} />
                {/each}
            </div>
        {/each}
        </div>
        </Col>
    </Row>
</Container>

<style>
    .cluster-shards {
        display: flex;
        align-items: center;
        gap: 5px;
        flex-wrap: wrap;
        margin-bottom: 0.5em;
    }

    .cluster-grid {
        display: grid;
        grid-template-columns: 100%;
        gap: 0.5em;
    }

    @media (min-width: 576px) {
        .cluster-card {
            text-align: right;
            margin-bottom: 0.5em;
        }

        .cluster-text {
            line-height: 3em;
        }

        .cluster-grid {
            grid-template-columns: max-content 1fr;
        }
    }
</style>
