<script lang="ts">
    import { Card, CardHeader, CardBody, Collapse, Tooltip } from 'sveltestrap';
    import { Member, Group } from '../../api/types';
    import { link } from 'svelte-navigator';

    import FaLock from 'svelte-icons/fa/FaLock.svelte';
    import FaUserCircle from 'svelte-icons/fa/FaUserCircle.svelte';
    import FaUsers from 'svelte-icons/fa/FaUsers.svelte'

    import MemberBody from '../member/Body.svelte';
    import GroupBody from '../group/Body.svelte';
    import CardsHeader from '../CardsHeader.svelte';

    let settings = JSON.parse(localStorage.getItem("pk-settings"));
    
    export let list: Member[]|Group[];
    export let members: Member[] = [];
    export let groups: Group[] = [];
    
    export let isPublic: boolean;
    export let itemType: string;
    export let itemsPerPage: number;
    export let currentPage: number;
    export let fullLength: number;

    export let openByDefault = false;

    export let searchBy = "name";
    export let sortBy = "name";

    $: indexStart = itemsPerPage * (currentPage - 1);

    function getItemLink(item: Member | Group): string {
        let url: string;

        if (!isPublic) url = "/dash/";
        else url = "/profile/";
        
        if (itemType === "member") url += "m/";
        else if (itemType === "group") url += "g/";

        url += item.id;

        return url;
    }

    function skipToNextItem(event, index: number) {
        let el;

        if (event.key === "ArrowDown") {
            if (index + 1 < indexStart + itemsPerPage && index + 1 < fullLength) el = document.getElementById(`${itemType}-card-${index + 1}`);
            else el = document.getElementById(`${itemType}-card-${indexStart}`);
        }

        if (event.key === "ArrowUp") {
            if (index - 1 >= indexStart) el = document.getElementById(`${itemType}-card-${index - 1}`);
            else if (fullLength <= indexStart + itemsPerPage) el = document.getElementById(`${itemType}-card-${fullLength - 1}`);
            else el = document.getElementById(`${itemType}-card-${indexStart + itemsPerPage - 1}`);
        }

        if (el) {
            event.preventDefault();
            el.focus();
        }
    }

    let isOpen = {};

    function toggleCard(index: string) {
        isOpen[index] = isOpen[index] || {};
        if (isOpen[index] === true) {
            isOpen[index] = false;
        } else {
            isOpen[index] = true;
        }
    }

    function getShortLink(id: string) {
        let url = "https://pk.mt"

        if (itemType === "member") url += "/m/"
        else if (itemType === "group") url += "/g/"

        url += id;

        return url;
    }

    let copiedArray = [];

    async function copyShortLink(index: number, id: string, event?) {
        if (event) {
            if (event.key !== "Tab") event.preventDefault();
            event.stopPropagation();

            let ctrlDown = event.ctrlKey||event.metaKey; // mac support
            if (!(ctrlDown && event.key === "c") && event.key !== "Enter") return;
        }
        try {
            await navigator.clipboard.writeText(getShortLink(id));
            
            copiedArray[index] = true;
            await new Promise(resolve => setTimeout(resolve, 2000));
            copiedArray[index] = false;
        } catch (error) {
            console.log(error);
        }
    }
</script>

{#if !openByDefault && (settings && settings.accessibility ? (!settings.accessibility.expandedcards && !settings.accessibility.pagelinks) : true)}
    <div class="mb-3">    
    {#each list as item, index (item.uuid)}
        <Card>
            <h2 class="accordion-header">
                <button class="w-100 accordion-button collapsed card-header" id={`${itemType}-card-${indexStart + index}`} on:click={() => toggleCard(item.uuid)} on:keydown={(e) => skipToNextItem(e, indexStart + index)}>
                    <CardsHeader {item} {sortBy} {searchBy}>
                        <div slot="icon" style="cursor: pointer;" id={`${itemType}-copy-${item.id}-${indexStart + index}`} on:click|stopPropagation={() => copyShortLink(indexStart + index, item.id)} on:keydown={(e) => copyShortLink(indexStart + index, item.id, e)} tabindex={0} >
                            {#if isPublic || item.privacy.visibility === "public"}
                            {#if itemType === "member"}
                            <FaUserCircle />
                            {:else if itemType === "group"}
                            <FaUsers />
                            {/if}
                            {:else}
                            <FaLock />
                            {/if}
                        </div>
                    </CardsHeader>
                    <Tooltip placement="top" target={`${itemType}-copy-${item.id}-${indexStart + index}`}>{copiedArray[indexStart + index] ? "Copied!" : "Copy public link"}</Tooltip>
                </button>
            </h2>
            <Collapse isOpen={isOpen[item.uuid]}>
                <CardBody>
                    {#if itemType === "member"}
                    <MemberBody on:update on:deletion bind:isPublic groups={groups} member={item} />
                    {:else if itemType === "group"}
                    <GroupBody on:update on:deletion bind:isPublic {members} group={item} />
                    {/if}
                </CardBody>
            </Collapse>
        </Card>
    {/each}
    </div>
{:else if openByDefault || settings.accessibility.expandedcards}
    {#each list as item, index (item.id + index)}
    <Card class="mb-3">
        <div class="accordion-button collapsed p-0" id={`${itemType}-card-${indexStart + index}`} on:keydown={(e) => skipToNextItem(e, indexStart + index)} tabindex={0}>
            <CardHeader class="w-100">
                <CardsHeader {item} {sortBy} {searchBy}>
                    <div slot="icon" style="cursor: pointer;" id={`${itemType}-copy-${item.id}-${indexStart + index}`} on:click|stopPropagation={() => copyShortLink(indexStart + index, item.id)} on:keydown|stopPropagation={(e) => copyShortLink(indexStart + index, item.id, e)} tabindex={0} >
                        {#if isPublic || item.privacy.visibility === "public"}
                        {#if itemType === "member"}
                        <FaUserCircle />
                        {:else if itemType === "group"}
                        <FaUsers />
                        {/if}
                        {:else}
                        <FaLock />
                        {/if}
                    </div>
                </CardsHeader>
                <Tooltip placement="top" target={`${itemType}-copy-${item.id}-${indexStart + index}`}>{copiedArray[indexStart + index] ? "Copied!" : "Copy public link"}</Tooltip>
            </CardHeader>
        </div>
        <CardBody>
            {#if itemType === "member"}
            <MemberBody on:update on:deletion bind:isPublic groups={groups} member={item} />
            {:else if itemType === "group"}
            <GroupBody on:update on:deletion bind:isPublic {members} group={item} />
            {/if}
        </CardBody>
    </Card>
    {/each}
{:else}
    <div class="my-3">
    {#each list as item, index (item.id + index)}
    <Card>
        <a class="accordion-button collapsed" style="text-decoration: none;" href={getItemLink(item)} id={`${itemType}-card-${indexStart + index}`} on:keydown={(e) => skipToNextItem(e, indexStart + index)} use:link >
            <CardsHeader {item}>
                <div slot="icon" style="cursor: pointer;" id={`${itemType}-copy-${item.id}-${indexStart + index}`} on:click|stopPropagation={() => copyShortLink(indexStart + index, item.id)} on:keydown|stopPropagation={(e) => copyShortLink(indexStart + index, item.id, e)} tabindex={0} >
                    {#if isPublic || item.privacy.visibility === "public"}
                    {#if itemType === "member"}
                    <FaUserCircle />
                    {:else if itemType === "group"}
                    <FaUsers />
                    {/if}
                    {:else}
                    <FaLock />
                    {/if}
                </div>
            </CardsHeader>
            <Tooltip placement="top" target={`${itemType}-copy-${item.id}-${indexStart + index}`}>{copiedArray[indexStart + index] ? "Copied!" : "Copy public link"}</Tooltip>
        </a>
    </Card>
    {/each}
    </div>
{/if}