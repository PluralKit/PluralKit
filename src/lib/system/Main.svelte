<script lang="ts">
    import { Card, CardBody } from 'sveltestrap';
    import CardsHeader from '../CardsHeader.svelte';
    import SystemBody from './Body.svelte';
    import SystemPrivacy from './Privacy.svelte';
    import type Sys from '../../api/system';

    export let user: Sys;
    export let isPublic = true;

    let editMode = false;
</script>

<Card class="mb-4">
    <CardsHeader bind:item={user}/>
    <CardBody style="border-left: 4px solid #{user.color}">
        {#if !editMode}
        <SystemBody bind:user={user} bind:editMode={editMode}/>
        {:else}
        hehe
        {/if}
    </CardBody>
</Card>

{#if !isPublic}
    <SystemPrivacy bind:user={user} />
{/if}