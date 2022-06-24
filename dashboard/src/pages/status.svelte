<script lang="ts">
    import { Container, Row, Col, Card, CardHeader, CardTitle, CardBody, Input, Button } from 'sveltestrap';
    import FaInfoCircle from 'svelte-icons/fa/FaInfoCircle.svelte'
    import ShardItem from '../lib/shard.svelte';

    import api from '../api';

    let hover = null;

    let message = "Loading...";
    let shards = [];
    let clusters = {};
    let pingAverage = "";
    let currentCommitMsg = "";

    let foundShard = {
        id: 1,
        status: 1,
        ping:"",
        disconnection_count:0,
        last_connection:0,
        last_heartbeat:0.
    };
    foundShard = null;

    let findShardInput = "";
    let valid = false;

    const get = async () => {
        const pkdata = await api().private.meta.get();
        let data = pkdata.shards.sort((x, y) => (x.id > y.id) ? 1 : -1);
        let pings = 0;
        data = data.map(shard => {
            pings += shard.ping;
            shard.last_connection = new Date(Number(shard.last_connection) * 1000).toUTCString().match(/([0-9][0-9]:[0-9][0-9]:[0-9][0-9])/)?.shift()
            shard.last_heartbeat = new Date(Number(shard.last_heartbeat) * 1000).toUTCString().match(/([0-9][0-9]:[0-9][0-9]:[0-9][0-9])/)?.shift()
            return shard;
        });

        currentCommitMsg = `Current Git commit: <a href="https://github.com/PluralKit/PluralKit/commit/${pkdata.version}">${pkdata.version.slice(0,7)}</a>`;

        if (data[0].cluster_id === 0) {
            let clusterData = {};
            data.forEach(shard => {
                if (clusterData[shard.cluster_id] === undefined) clusterData[shard.cluster_id] = [];
                clusterData[shard.cluster_id].push(shard);
            });
            clusters = clusterData;
        }

        shards = data;
        pingAverage = Math.trunc(pings / shards.length).toString();

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
                            <ShardItem shard={shard} bind:hover={hover} />
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
                <ShardItem shard={shard} bind:hover={hover} />
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