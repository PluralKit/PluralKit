<script lang="ts">
    import { Alert, Row, Col, Spinner, Button } from 'sveltestrap';
    import { onMount } from 'svelte';
    import { useParams } from 'svelte-navigator';

    import NewMember from '../member/NewMember.svelte';
    import NewGroup from '../group/NewGroup.svelte';
    import ListPagination from '../ListPagination.svelte';
    import ListControl from './ListControl.svelte';
    import ListSearch from './ListSearch.svelte';
    import CardsList from './CardsList.svelte';

    import { Member, Group } from '../../api/types';
    import api from '../../api';

    export let members: Member[] = [];
    export let groups: Group[] = [];

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

    let itemsPerPageValue = settings && settings.accessibility && settings.accessibility.expandedcards ? "10" : "25";
    $: itemsPerPage = parseInt(itemsPerPageValue);

    $: indexOfLastItem = currentPage * itemsPerPage;
    $: indexOfFirstItem = indexOfLastItem - itemsPerPage;
    $: pageAmount = Math.ceil(processedList.length / itemsPerPage);

    $: slicedList = processedList.slice(indexOfFirstItem, indexOfLastItem);

    export let isPublic: boolean;
    export let itemType: string;

    let searchValue: string = "";
    let searchBy: string = "name"; 

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

    /* function updateList(event: any) {
        list = list.map(member => member.id !== event.detail.id ? member : event.detail);
    } */

    /* function updateGroups(event: any) {
        groups = event.detail;
    } */

    function updateDelete(event: any) {
        if (itemType === "member") {
            members = members.filter(m => m.id !== event.detail);
            list = members;
        } else if (itemType === "group") {
            groups = groups.filter(g => g.id !== event.detail);
            list = groups;
        }
    }
</script>

<ListControl {itemType} {isPublic} {memberList} {groups} {groupList} {list} bind:finalList={processedList} bind:searchValue bind:searchBy bind:itemsPerPageValue bind:currentPage />

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
<ListSearch bind:searchBy bind:searchValue on:refresh={fetchList} />

<ListPagination bind:currentPage {pageAmount} />

{#if !err && !isPublic}
    {#if itemType === "member"}
    <NewMember on:create={addItemToList} />
    {:else if itemType === "group"}
    <NewGroup on:create={addItemToList} />
    {/if}
{/if}
<CardsList on:deletion={updateDelete} bind:list={slicedList} bind:groups bind:members isPublic={isPublic} itemType={itemType} itemsPerPage={itemsPerPage} currentPage={currentPage} fullLength={list.length} />
<ListPagination bind:currentPage {pageAmount} />
{/if}
