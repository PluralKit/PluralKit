<template>
    <div class="wrapper">
        <div class="shard" :id="shard.id" :style="color" @mouseover="hover = true">
            {{ shard.id }}
        </div>
        <div class="more-info" :style="moreInfoStyle">
            <div>
                <h3>Shard {{ shard.id }}</h3>
                <br>
                <span>Status: {{ shard.status }}</span><br>
                <span>Latency: {{ shard.ping }}ms</span><br>
                <span>Last connection: {{ new Date(shard.last_connection).toGMTString().match(/([0-9][0-9]:[0-9][0-9]:[0-9][0-9])/)[0] }}</span><br>
                <span>Last heartbeat: {{ new Date(shard.last_heartbeat).toGMTString().match(/([0-9][0-9]:[0-9][0-9]:[0-9][0-9])/)[0] }}</span><br>
            </div>
        </div>
    </div>
</template>

<script>
export default {
    data: () => ({ hover: false }),
    props: [ "shard" ],
    computed: {
        color() {
            // shard is down
            if (this.shard.status != "up") return "background-color: #000;";
            // shard latency is < 250ms: OK!
            else if (this.shard.ping < 300) return "background-color: #00cc00;";
            // shard latency is 250ms < ping < 600ms: slow, but OK
            else if (this.shard.ping < 600) return "background-color: #da9317;";
            // shard latency is >600ms, this might be problematic
            else return "background-color: #cc0000;"
        },
        moreInfoStyle() {
            if (this.hover == false) return "color: #ffa;";
            const shard = document.getElementById(this.shard.id);
            if (shard == null) return "color: #ffb;";
            const rect = shard.getBoundingClientRect();
            const style = `left: ${Math.trunc(rect.left) - 50}px; top: ${Math.trunc(rect.top) + 50}px;`;
            return style;
        }
    },
}
</script>

<style>
.wrapper {
    height: 55px;
    width: 55px;
    display: block;
    float: left;
}

.shard {
    color: #fff;
    display: block;
    float: left;
    display: flex;
    flex-direction: column;
    align-items: center;
    text-align: center;
    justify-content: center;
    z-index: 1;

    height: 50px;
    width: 50px;
    margin-right: 5px;
    margin-bottom: 5px;
    border-radius: 2px;

    -webkit-touch-callout: none; /* iOS Safari */
      -webkit-user-select: none; /* Safari */
       -khtml-user-select: none; /* Konqueror HTML */
         -moz-user-select: none; /* Old versions of Firefox */
          -ms-user-select: none; /* Internet Explorer/Edge */
              user-select: none; /* Non-prefixed version, currently
                                    supported by Chrome, Edge, Opera and Firefox */

}

.more-info {
    display: none;
    position: absolute;
    will-change: transform;
    z-index: 2;

    min-height: 150px;
    width: 200px;
    border-radius: 5px;

    background-color: #333;
    color: #fff;
    opacity: 95%;

    text-align: center;
}

.more-info div {
    margin-top: 10px;
}

.shard:hover~.more-info {
    display: block;
}

span {
    margin-top: 10px;
}
</style>