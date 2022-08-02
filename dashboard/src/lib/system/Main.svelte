<script lang="ts">
    import { Card, CardBody, CardHeader, Tooltip } from 'sveltestrap';
    import FaAddressCard from 'svelte-icons/fa/FaAddressCard.svelte'
    import CardsHeader from '../CardsHeader.svelte';
    import Body from './Body.svelte';
    import Privacy from './Privacy.svelte';
    import Edit from './Edit.svelte';

    import { System } from '../../api/types';

    export let user: System;
    export let isPublic = true;

    let editMode = false;
    let copied = false;

    async function copyShortLink(event?) {
        if (event) {
            let ctrlDown = event.ctrlKey||event.metaKey; // mac support
            if (!(ctrlDown && event.key === "c") && event.key !== "Enter") return;
        }
        try {
            await navigator.clipboard.writeText(`https://pk.mt/s/${user.id}`);
            copied = true;
            await new Promise(resolve => setTimeout(resolve, 2000));
            copied = false;
        } catch (error) {
            console.log(error);
        }
    }
</script>

<Card class="mb-4">
    <CardHeader>
        <CardsHeader bind:item={user}>
            <div slot="icon" style="cursor: pointer;" id={`copy-${user.id}`} on:click|stopPropagation={() => copyShortLink()} on:keydown|stopPropagation={(e) => copyShortLink(e)} tabindex={0} >
                <FaAddressCard />
            </div>
        </CardsHeader>
        <Tooltip placement="top" target={`copy-${user.id}`}>{copied ? "Copied!" : "Copy public link"}</Tooltip>
    </CardHeader>
    <CardBody style="border-left: 4px solid #{user.color}">
        {#if !editMode}
        <Body bind:user bind:editMode bind:isPublic/>
        {:else}
        <Edit bind:user bind:editMode />
        {/if}
    </CardBody>
</Card>

{#if !isPublic}
    <Privacy bind:user />
{/if}