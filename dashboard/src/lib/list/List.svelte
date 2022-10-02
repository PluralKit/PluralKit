<script lang="ts">
    import { Alert, Row, Col, Spinner, Button } from 'sveltestrap';
    import { onMount } from 'svelte';
    import { useParams } from 'svelte-navigator';

    import NewMember from '../member/NewMember.svelte';
    import NewGroup from '../group/NewGroup.svelte';
    import ListPagination from '../ListPagination.svelte';
    import ListControl from './ListControl.svelte';
    import ListSearch from './ListSearch.svelte';
    import ListView from './ListView.svelte';
    import CardView from './CardView.svelte';

    import { Member, Group } from '../../api/types';
    import api from '../../api';

    export let members: Member[] = [];
    export let groups: Group[] = [];
    
    export let view: string = "list";

    export let isDash = false;

    let list: Member[] | Group[] = [];
    let processedList: Member[] | Group[] = [];

    $: groupList = groups && groups.map(function(group) { return {name: group.name, shortid: group.id, id: group.uuid, members: group.members, display_name: group.display_name}; }).sort((a, b) => a.name.localeCompare(b.name));
    $: memberList = members && members.map(function(member) { return {name: member.name, shortid: member.id, id: member.uuid, display_name: member.display_name}; }).sort((a, b) => a.name.localeCompare(b.name));

    let token = localStorage.getItem("pk-token");
    let listLoading = true;
    let err: string;

    let settings = JSON.parse(localStorage.getItem("pk-settings"));
    
    let pageAmount: number;
    let currentPage: number = 1;

    let itemsPerPageValue;
    $: {
        if (view === "card") itemsPerPageValue = "24";
    
        else if (settings && settings.accessibility && settings.accessibility.expandedcards) itemsPerPageValue = "10";
        else itemsPerPageValue = "25";
    }
    
    $: itemsPerPage = parseInt(itemsPerPageValue);

    $: indexOfLastItem = currentPage * itemsPerPage;
    $: indexOfFirstItem = indexOfLastItem - itemsPerPage;
    $: pageAmount = Math.ceil(processedList.length / itemsPerPage);

    $: slicedList = processedList.slice(indexOfFirstItem, indexOfLastItem);

    export let isPublic: boolean;
    export let itemType: string;

    let searchValue: string = "";
    let searchBy: string = "name";
    let sortBy: string = "name";

    let params = useParams();
    $: id = $params.id;

    onMount(() => {
        if (token || isPublic) fetchList();
    });

    async function fetchList() {
        err = "";
        listLoading = true;
        try {
            if (itemType === "member") { 
                const res: Member[] = await api().systems(isPublic ? id : "@me").members.get({ auth: !isPublic });
                members = res;
                list = res;
            }
            else if (itemType === "group") {
                const res: Group[] = await api().systems(isPublic ? id : "@me").groups.get({ auth: !isPublic, query: { with_members: !isPublic } });
                groups = res;
                list = res;
            }
            else throw new Error(`Unknown list type ${itemType}`);
        } catch (error) {
            console.log(error);
            err = error.message;
        }
        listLoading = false;
    }

    function addItemToList(event: any) {
        if (itemType === "member") {
            members.push(event.detail);
            list = members;
        } else if (itemType === "group") {
            groups.push(event.detail);
            list = groups;
        }
    }

    function updateDelete(event: any) {
        if (itemType === "member") {
            members = members.filter(m => m.id !== event.detail);
            list = members;
        } else if (itemType === "group") {
            groups = groups.filter(g => g.id !== event.detail);
            list = groups;
        }
    }

    function update(event: any) {
        if (itemType === "member") {
            members = members.map(m => m.id === event.detail.id ? m = event.detail : m);
            list = members;
        } else if (itemType === "group") {
            groups = groups.map(g => g.id === event.detail.id ? g = event.detail : g);
            list = groups;
        }
    }

</script>

<ListControl on:viewChange {itemType} {isPublic} {memberList} {groups} {groupList} {list} bind:finalList={processedList} bind:searchValue bind:searchBy bind:sortBy bind:itemsPerPageValue bind:currentPage bind:view />

{#if listLoading && !err}
    <div class="mx-auto text-center">
        <Spinner class="d-inline-block" />
    </div>
{:else if err}
<Row>
    <Col xs={12} lg={10}>
        <Alert color="danger">{err}</Alert>
    </Col>
    <Col xs={12} lg={2}>
        <Button class="w-100 mb-3" color="primary" on:click={fetchList} aria-label="refresh member list">Refresh</Button>
    </Col>
</Row>
{:else}
<span class="itemcounter">{processedList.length} {itemType}s ({slicedList.length} shown)</span>
<ListSearch bind:searchBy bind:searchValue on:refresh={fetchList} />

<ListPagination bind:currentPage {pageAmount} />

{#if !err && !isPublic}
    {#if itemType === "member"}
    <NewMember on:create={addItemToList} />
    {:else if itemType === "group"}
    <NewGroup on:create={addItemToList} />
    {/if}
{/if}
{#if view === "card"}
    <CardView on:update={update} list={slicedList} {groups} {members} {itemType} {sortBy} {searchBy} {isPublic} {isDash} />
{:else if view === "tiny"}
    tiny!
{:else}
<ListView on:update={update} on:deletion={updateDelete} list={slicedList} {groups} {members} {isPublic} {itemType} {itemsPerPage} {currentPage} {sortBy} {searchBy} fullLength={list.length} />
{/if}
<ListPagination bind:currentPage {pageAmount} />
{/if}

<style>
    .itemcounter {
        width: 100%;
        text-align: center;
        display: inline-block;
        margin-bottom: 0.5rem;
    }
</style>