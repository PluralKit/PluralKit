<script lang="ts">
	import { Tooltip } from 'sveltestrap';

	export let shard = {
		id: 1,
		status: "",
		ping:0,
		disconnection_count:0,
		last_connection:0,
		last_heartbeat:0.
	};
	
	let color = "background-color: #fff";

	// shard is down
	// todo: check if last heartbeat is really recent, since database up/down status can get out of sync
	if (shard.status != "up") color = "background-color: #000;";
	// shard latency is < 250ms: OK!
	else if (shard.ping < 300) color = "background-color: #00cc00;";
	// shard latency is 250ms < ping < 600ms: slow, but OK
	else if (shard.ping < 600) color = "background-color: #da9317;";
	// shard latency is >600ms, this might be problematic
	else color = "background-color: #cc0000;"
</script>

<div class="wrapper">
	<div
		class="shard" id={`shard-${shard.id.toString()}`}
		style={color}
	>{ shard.id }</div>
	<Tooltip target={`shard-${shard.id.toString()}`} placement="bottom">
		<br>
		<h3>Shard { shard.id }</h3>
		<br>
		<span>Status: <b>{ shard.status }</b></span><br>
		<span>Latency: { shard.ping }ms</span><br>
		<span>Disconnection count: { shard.disconnection_count }</span><br>
		<span>Last connection: { shard.last_connection }</span><br>
		<span>Last heartbeat: { shard.last_heartbeat }</span><br>
		<br>
	</Tooltip>
</div>

<style>
	.wrapper {
		position: relative;
		display: inline-block;
	}
	.shard:hover {
		cursor: pointer;
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
		height: 3em;
		width: 3em;
		border-radius: 2px;
		-webkit-touch-callout: none; /* iOS Safari */
		  -webkit-user-select: none; /* Safari */
		   -khtml-user-select: none; /* Konqueror HTML */
			 -moz-user-select: none; /* Old versions of Firefox */
			  -ms-user-select: none; /* Internet Explorer/Edge */
				  user-select: none; /* Non-prefixed version, currently
										supported by Chrome, Edge, Opera and Firefox */
	}
</style>