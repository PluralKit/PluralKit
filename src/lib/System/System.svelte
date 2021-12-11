<script lang="ts">
    import { Card, CardBody } from 'sveltestrap';
    import CardsHeader from '../CardsHeader.svelte';
    import SystemBody from './SystemBody.svelte';

    export let user;
    let editMode = false;

</script>

<Card>
    <CardsHeader bind:item={user}/>
    <CardBody style="border-left: 4px solid #{user.color}">
        {#if !editMode}
        <SystemBody bind:user={user} bind:editMode={editMode}/>
        {:else}
        hehe
        {/if}
    </CardBody>
</Card>