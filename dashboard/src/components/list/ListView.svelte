<script lang="ts">
    import { Card, CardHeader, CardBody, Collapse, Tooltip } from 'sveltestrap';
    import type { Member, Group } from '../../api/types';
    import { link } from 'svelte-navigator';

    import FaLock from 'svelte-icons/fa/FaLock.svelte';
    import FaUserCircle from 'svelte-icons/fa/FaUserCircle.svelte';
    import FaUsers from 'svelte-icons/fa/FaUsers.svelte'

    import MemberBody from '../member/Body.svelte';
    import GroupBody from '../group/Body.svelte';
    import CardsHeader from '../common/CardsHeader.svelte';
    import { defaultListOptions, type List, type ListOptions, type PageOptions } from './types';

    let settings = JSON.parse(localStorage.getItem("pk-settings"));
    
    export let options: ListOptions = JSON.parse(JSON.stringify(defaultListOptions));
    export let otherList: List <Member|Group>;
    export let lists: List<Member|Group>;
    export let pageOptions: PageOptions;

    function getItemLink(item: Member | Group): string {
        let url: string;

        if (!pageOptions.isPublic) url = "/dash/";
        else url = "/profile/";
        
        if (pageOptions.type === "member") url += "m/";
        else if (pageOptions.type === "group") url += "g/";

        url += item.id;

        return url;
    }

    $: indexStart = pageOptions.itemsPerPage * (pageOptions.currentPage - 1);

    function skipToNextItem(event, index: number) {
        let el;

        if (event.key === "ArrowDown") {
            if (index + 1 < indexStart + pageOptions.itemsPerPage && index + 1 < lists.processedList.length) el = document.getElementById(`${pageOptions.type}-card-${index + 1}`);
            else el = document.getElementById(`${pageOptions.type}-card-${indexStart}`);
        }

        if (event.key === "ArrowUp") {
            if (index - 1 >= indexStart) el = document.getElementById(`${pageOptions.type}-card-${index - 1}`);
            else if (lists.processedList.length <= indexStart + pageOptions.itemsPerPage) el = document.getElementById(`${pageOptions.type}-card-${lists.processedList.length - 1}`);
            else el = document.getElementById(`${pageOptions.type}-card-${indexStart + pageOptions.itemsPerPage - 1}`);
        }

        if (el) {
            event.preventDefault();
            el.focus();
        }
    }

    let isOpen = {};

    function toggleCard(index: string, count: number) {

        if (pageOptions.randomized) {
            let newIndex = index + '-' + pageOptions.currentPage + '-' + count;

            isOpen[newIndex] = isOpen[newIndex] || {};
            if (isOpen[newIndex] === true) {
                isOpen[newIndex] = false;
            } else {
                isOpen[newIndex] = true;
            }
            return;
        }

        isOpen[index] = isOpen[index] || {};
        if (isOpen[index] === true) {
            isOpen[index] = false;
        } else {
            isOpen[index] = true;
        }
    }

    function getShortLink(id: string) {
        let url = "https://pk.mt"

        if (pageOptions.type === "member") url += "/m/"
        else if (pageOptions.type === "group") url += "/g/"

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

{#if (settings && settings.accessibility ? (!settings.accessibility.expandedcards && !settings.accessibility.pagelinks) : true)}
    <div class="mb-3 accordion">    
    {#each lists.currentPage as item, index (pageOptions.randomized ? item.uuid + '-' + index : item.uuid)}
        <Card style="border-radius: 0;">
            <h2 class="accordion-header">
                <button class="w-100 accordion-button collapsed bg-transparent" id={`${pageOptions.type}-card-${indexStart + index}`} on:click={() => toggleCard(item.uuid, index)} on:keydown={(e) => skipToNextItem(e, indexStart + index)}>
                    <CardsHeader {item} sortBy={options.sort}>
                        <div slot="icon" style="cursor: pointer;" id={`${pageOptions.type}-copy-${item.id}-${indexStart + index}`} on:click|stopPropagation={() => copyShortLink(indexStart + index, item.id)} on:keydown={(e) => copyShortLink(indexStart + index, item.id, e)} tabindex={0} >
                            {#if item.privacy && item.privacy.visibility === "private"}
                            {#if pageOptions.type === "member"}
                            <FaLock />
                            {:else if pageOptions.type === "group"}
                            <FaUsers />
                            {/if}
                            {:else}
                            <FaUserCircle />
                            {/if}
                        </div>
                    </CardsHeader>
                    <Tooltip placement="top" target={`${pageOptions.type}-copy-${item.id}-${indexStart + index}`}>{copiedArray[indexStart + index] ? "Copied!" : "Copy public link"}</Tooltip>
                </button>
            </h2>
            <Collapse isOpen={pageOptions.randomized ? isOpen[item.uuid + '-' + pageOptions.currentPage + '-' + index] : isOpen[item.uuid]}>
                <CardBody class="border-top">
                    {#if pageOptions.type === "member"}
                    <MemberBody on:update on:deletion bind:isPublic={pageOptions.isPublic} groups={otherList.rawList} member={item} />
                    {:else if pageOptions.type === "group"}
                    <GroupBody on:update on:deletion bind:isPublic={pageOptions.isPublic} members={otherList.rawList} group={item} />
                    {/if}
                </CardBody>
            </Collapse>
        </Card>
    {/each}
    </div>
{:else if settings.accessibility.expandedcards}
    {#each lists.currentPage as item, index (pageOptions.randomized ? item.uuid + '-' + index : item.uuid)}
    <Card class="mb-3">
        <div class="accordion-button collapsed p-0" id={`${pageOptions.type}-card-${indexStart + index}`} on:keydown={(e) => skipToNextItem(e, indexStart + index)} tabindex={0}>
            <CardHeader class="w-100">
                <CardsHeader {item} sortBy={options.sort}>
                    <div slot="icon" style="cursor: pointer;" id={`${pageOptions.type}-copy-${item.id}-${indexStart + index}`} on:click|stopPropagation={() => copyShortLink(indexStart + index, item.id)} on:keydown|stopPropagation={(e) => copyShortLink(indexStart + index, item.id, e)} tabindex={0} >
                        {#if item.privacy && item.privacy.visibility === "private"}
                        {#if pageOptions.type === "member"}
                        <FaLock />
                        {:else if pageOptions.type === "group"}
                        <FaUsers />
                        {/if}
                        {:else}
                        <FaUserCircle />
                        {/if}
                    </div>
                </CardsHeader>
                <Tooltip placement="top" target={`${pageOptions.type}-copy-${item.id}-${indexStart + index}`}>{copiedArray[indexStart + index] ? "Copied!" : "Copy public link"}</Tooltip>
            </CardHeader>
        </div>
        <CardBody>
            {#if pageOptions.type === "member"}
            <MemberBody on:update on:deletion bind:isPublic={pageOptions.isPublic} groups={otherList.rawList} member={item} />
            {:else if pageOptions.type === "group"}
            <GroupBody on:update on:deletion bind:isPublic={pageOptions.isPublic}  members={otherList.rawList} group={item} />
            {/if}
        </CardBody>
    </Card>
    {/each}
{:else}
    <div class="my-3">
    {#each lists.currentPage as item, index(pageOptions.randomized ? item.uuid + '-' + index : item.uuid)}
    <Card style="border-radius: 0;">
        <a class="accordion-button collapsed bg-transparent" style="text-decoration: none;" href={getItemLink(item)} id={`${pageOptions.type}-card-${indexStart + index}`} on:keydown={(e) => skipToNextItem(e, indexStart + index)} use:link >
            <CardsHeader {item} sortBy={options.sort}>
                <div slot="icon" style="cursor: pointer;" id={`${pageOptions.type}-copy-${item.id}-${indexStart + index}`} on:click|stopPropagation={() => copyShortLink(indexStart + index, item.id)} on:keydown|stopPropagation={(e) => copyShortLink(indexStart + index, item.id, e)} tabindex={0} >
                    {#if item.privacy && item.privacy.visibility === "private"}
                    {#if pageOptions.type === "member"}
                    <FaLock />
                    {:else if pageOptions.type === "group"}
                    <FaUsers />
                    {/if}
                    {:else}
                    <FaUserCircle />
                    {/if}
                </div>
            </CardsHeader>
            <Tooltip placement="top" target={`${pageOptions.type}-copy-${item.id}-${indexStart + index}`}>{copiedArray[indexStart + index] ? "Copied!" : "Copy public link"}</Tooltip>
        </a>
    </Card>
    {/each}
    </div>
{/if}