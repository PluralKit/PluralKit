<script lang="ts">
    import { Row, Tooltip } from 'sveltestrap';
    import FaLock from 'svelte-icons/fa/FaLock.svelte';
    import FaUserCircle from 'svelte-icons/fa/FaUserCircle.svelte';
    import FaUsers from 'svelte-icons/fa/FaUsers.svelte'

    import { Member, Group } from '../../api/types';
    import MemberCard from '../member/CardView.svelte';
    import GroupCard from '../group/CardView.svelte';

    export let list: Member[]|Group[];
    export let groups: Group[] = [];
    export let members: Group[] = [];
    
    export let itemType: string;

    export let searchBy = "name";
    export let sortBy = "name";
    export let isPublic = false;
    export let isDash = false;

    let copiedItems = {};

    function getShortLink(id: string) {
        let url = "https://pk.mt"

        if (itemType === "member") url += "/m/"
        else if (itemType === "group") url += "/g/"

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

<Row>
    {#if itemType === "member"}
        {#each list as item (item.uuid)}
        <div class="col-12 col-sm-6 col-md-4 col-lg-3 mx-auto mx-sm-0 dont-squish">
            <MemberCard on:update member={item} {searchBy} {sortBy} {groups} {isPublic} {isDash}>
                    <div slot="icon" style="width: auto; height: 1em; cursor: pointer;" id={`${itemType}-copy-${item.uuid}`} on:click|stopPropagation={() => copyShortLink(item.uuid, item.id)} on:keydown={(e) => copyShortLink(item.uuid, item.id, e)} tabindex={0} >
                        {#if item.privacy && item.privacy.visibility === "private"}
                            <FaLock />
                        {:else}
                            <FaUserCircle />
                        {/if}
                    </div>
            </MemberCard>
            <Tooltip placement="top" target={`${itemType}-copy-${item.uuid}`}>{copiedItems[item.uuid] ? "Copied!" : "Copy public link"}</Tooltip>
        </div>
        {/each}
    {:else if itemType === "group"}
    {#each list as item (item.uuid)}
        <div class="col-12 col-sm-6 col-md-4 col-lg-3 mx-auto mx-sm-0 dont-squish">
            <GroupCard group={item} {searchBy} {sortBy} {members} {isPublic} {isDash}>
                <div slot="icon" style="width: auto; height: 1em; cursor: pointer;" id={`${itemType}-copy-${item.uuid}`} on:click|stopPropagation={() => copyShortLink(item.uuid, item.id)} on:keydown={(e) => copyShortLink(item.uuid, item.id, e)} tabindex={0} >
                        {#if item.privacy && item.privacy.visibility === "private"}
                            <FaLock />
                        {:else}
                            <FaUsers />
                        {/if}
                    </div>
            </GroupCard>
            <Tooltip placement="top" target={`${itemType}-copy-${item.uuid}`}>{copiedItems[item.uuid] ? "Copied!" : "Copy public link"}</Tooltip>
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
</style>