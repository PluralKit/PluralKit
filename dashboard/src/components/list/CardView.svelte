<script lang="ts">
    import { Row, Tooltip } from 'sveltestrap';
    import FaLock from 'svelte-icons/fa/FaLock.svelte';
    import FaUserCircle from 'svelte-icons/fa/FaUserCircle.svelte';
    import FaUsers from 'svelte-icons/fa/FaUsers.svelte'

    import type { Member, Group } from '../../api/types';
    import MemberCard from '../member/CardView.svelte';
    import GroupCard from '../group/CardView.svelte';
    import type { PageOptions } from './types';

    export let pageOptions: PageOptions;
    export let currentList: Member[]|Group[];

    let copiedItems = {};

    function getShortLink(id: string) {
        let url = "https://pk.mt"

        if (pageOptions.type === "member") url += "/m/"
        else if (pageOptions.type === "group") url += "/g/"

        url += id;

        return url;
    }

    async function copyShortLink(index: string, id: string, event?) {
        if (event) {
            if (event.key !== "Tab") event.preventDefault();
            event.stopPropagation();

            let ctrlDown = event.ctrlKey||event.metaKey; // mac support
            if (!(ctrlDown && event.key === "c") && event.key !== "Enter") return;
        }
        try {
            await navigator.clipboard.writeText(getShortLink(id));
            
            copiedItems[index] = copiedItems[index] || false;
            copiedItems[index] = true;
            await new Promise(resolve => setTimeout(resolve, 2000));
            copiedItems[index] = false;
        } catch (error) {
            console.log(error);
        }
    }
</script>

<Row class="mx-4 mx-sm-5 mx-md-0">
    {#if pageOptions.type === "member"}
        {#each currentList as item (item.uuid)}
        <div class="col-12 col-md-6 col-lg-4 col-xxl-3 mx-auto mx-sm-0 dont-squish">
            <MemberCard on:update member={item} searchBy="name" sortBy="name" isPublic={pageOptions.isPublic} isDash={pageOptions.isMain}>
                    <button class="button-reset" slot="icon" style="width: auto; height: 1em; cursor: pointer;" id={`${pageOptions.type}-copy-${item.uuid}`} on:click|stopPropagation={() => copyShortLink(item.uuid, item.id)} on:keydown={(e) => copyShortLink(item.uuid, item.id, e)} tabindex={0} >
                        {#if item.privacy && item.privacy.visibility === "private"}
                            <FaLock />
                        {:else}
                            <FaUserCircle />
                        {/if}
                    </button>
            </MemberCard>
            <Tooltip placement="top" target={`${pageOptions.type}-copy-${item.uuid}`}>{copiedItems[item.uuid] ? "Copied!" : "Copy public link"}</Tooltip>
        </div>
        {/each}
    {:else if pageOptions.type === "group"}
    {#each currentList as item (item.uuid)}
        <div class="col-12 col-md-6 col-lg-4 col-xxl-3 mx-auto mx-sm-0 dont-squish">
            <GroupCard group={item} searchBy="name" sortBy="name" isPublic={pageOptions.isPublic} isDash={pageOptions.isMain}>
                <button class="button-reset" slot="icon" style="width: auto; height: 1em; cursor: pointer;" id={`${pageOptions.type}-copy-${item.uuid}`} on:click|stopPropagation={() => copyShortLink(item.uuid, item.id)} on:keydown={(e) => copyShortLink(item.uuid, item.id, e)} tabindex={0} >
                    {#if item.privacy && item.privacy.visibility === "private"}
                        <FaLock />
                    {:else}
                        <FaUsers />
                    {/if}
                </button>
            </GroupCard>
            <Tooltip placement="top" target={`${pageOptions.type}-copy-${item.uuid}`}>{copiedItems[item.uuid] ? "Copied!" : "Copy public link"}</Tooltip>
        </div>
        {/each}
    {/if}
</Row>

<style>
    @media (max-width: 576px) {
        .dont-squish {
            max-width: 24rem;
        }
    }

    .button-reset {
        background: none;
        color: inherit;
        border: none;
        padding: 0;
        font: inherit;
        cursor: pointer;
        outline: inherit;
    }
</style>