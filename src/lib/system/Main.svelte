<script lang="ts">
    import { Card, CardBody } from 'sveltestrap';
    import CardsHeader from '../CardsHeader.svelte';
    import SystemBody from './Body.svelte';
    import SystemPrivacy from './Privacy.svelte';
    import Edit from './Edit.svelte';
    import type Sys from '../../api/system';

    export let user: Sys;
    export let isPublic = true;
    let loading: boolean;

    let editMode = false;
</script>

<Card class="mb-4">
    <CardsHeader bind:item={user} bind:loading/>
    <CardBody style="border-left: 4px solid #{user.color}">
        {#if !editMode}
        <Body bind:user bind:editMode/>
        {:else}
        <Edit bind:user bind:editMode bind:loading />
        {/if}
    </CardBody>
</Card>

{#if !isPublic}
    <SystemPrivacy bind:user={user} />
{/if}