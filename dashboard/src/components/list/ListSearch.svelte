<script lang="ts">
import { createEventDispatcher } from "svelte";
import { Button, Col, Input, Row } from "sveltestrap";

export let searchValue: string;
export let searchBy: string;

const dispatch = createEventDispatcher();

function update() {
    dispatch("refresh");
}

</script>

<Row>
    <Col xs={12} lg={10}>
        <Input class="mb-3" bind:value={searchValue} placeholder="search by {searchBy}..."/>
    </Col>
    <Col xs={12} lg={2} class="mb-3 mb-lg-0">
        <Button class="w-100 mb-3" color="primary" on:click={() => update()} aria-label="refresh member list">Refresh</Button>
    </Col>
</Row>