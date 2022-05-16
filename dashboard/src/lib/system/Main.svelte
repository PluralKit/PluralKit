<script lang="ts">
    import { Card, CardBody, CardHeader } from 'sveltestrap';
    import FaAddressCard from 'svelte-icons/fa/FaAddressCard.svelte'
    import CardsHeader from '../CardsHeader.svelte';
    import Body from './Body.svelte';
    import Privacy from './Privacy.svelte';
    import Edit from './Edit.svelte';

    import { System } from '../../api/types';

    export let user: System;
    export let isPublic = true;
    let loading = false;

    let editMode = false;
</script>

<Card class="mb-4">
    <CardHeader>
        <CardsHeader bind:item={user} bind:loading>
            <FaAddressCard slot="icon" />
        </CardsHeader>
    </CardHeader>
    <CardBody style="border-left: 4px solid #{user.color}">
        {#if !editMode}
        <Body bind:user bind:editMode bind:isPublic/>
        {:else}
        <Edit bind:user bind:editMode bind:loading />
        {/if}
    </CardBody>
</Card>

{#if !isPublic}
    <Privacy bind:user />
{/if}