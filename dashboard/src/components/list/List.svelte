<script lang="ts">
    import { Alert, Row, Col, Spinner, Button } from 'sveltestrap';
    import { onMount } from 'svelte';
    import { useParams } from 'svelte-navigator';

    import NewMember from '../member/NewMember.svelte';
    import NewGroup from '../group/NewGroup.svelte';
    import ListPagination from '../common/ListPagination.svelte';
    import ListControl from './ListControl.svelte';
    import ListView from './ListView.svelte';
    import CardView from './CardView.svelte';

    import type { Member, Group } from '../../api/types';
    import api from '../../api';
    import type { ListOptions, List, PageOptions } from './types';
    import { createShortList, filterList, getPageAmount, paginateList } from './functions';

    export let options: ListOptions;
    export let pageOptions: PageOptions;
    export let lists: List<Member|Group>;
    export let otherList: List<Member|Group>;

    let token = localStorage.getItem("pk-token");
    let listLoading = true;
    let err: string;

    let settings = JSON.parse(localStorage.getItem("pk-settings"));
    let pageAmount = 1;

    if (pageOptions.view === "card") pageOptions.itemsPerPage = 24;
    else if (settings && settings.accessibility && settings.accessibility.expandedcards) pageOptions.itemsPerPage = 10;
    else pageOptions.itemsPerPage = 25;

    let params = useParams();
    $: id = $params.id;

    onMount(() => {
        if (token || pageOptions.isPublic) fetchList();
    });

    async function fetchList() {
        err = "";
        listLoading = true;
        try {
            if (pageOptions.type === "member") { 
                const res: Member[] = await api().systems(pageOptions.isPublic ? id : "@me").members.get({ auth: !pageOptions.isPublic });
                lists.rawList = res;
            }
            else if (pageOptions.type === "group") {
                const res: Group[] = await api().systems(pageOptions.isPublic ? id : "@me").groups.get({ auth: !pageOptions.isPublic, query: { with_members: !pageOptions.isPublic } });
                lists.rawList = res;
            }
            else throw new Error(`Unknown list type ${pageOptions.type}`);
        } catch (error) {
            console.log(error);
            err = error.message;
        }
        listLoading = false;
    }

    $: lists.shortGroups = createShortList(pageOptions.type === 'group' ? lists.rawList : otherList.rawList);
    $: lists.shortMembers = createShortList(pageOptions.type === 'member' ? lists.rawList : otherList.rawList);
    $: lists.processedList = filterList(lists.rawList, options, pageOptions.type);
    $: lists.currentPage = paginateList(lists.processedList, pageOptions);
    $: pageAmount = getPageAmount(lists.processedList, pageOptions);

    function addItemToList(event: any) {
        lists.rawList.push(event.detail);
        lists.rawList = lists.rawList;
    }

    function updateDelete(event: any) {
        lists.rawList = lists.rawList.filter(m => m.id !== event.detail);
    }

    function update(event: any) {
        lists.rawList = lists.rawList.map(m => m.id === event.detail.id ? m = event.detail : m);
    }

</script>

<ListControl on:viewChange bind:options bind:lists bind:pageOptions />

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
<span class="itemcounter">{lists.processedList.length} {pageOptions.type}s ({lists.currentPage.length} shown) <a href="#!" on:click={(e) => {e.preventDefault(); fetchList()}}>Refresh list</a></span>

<ListPagination bind:currentPage={pageOptions.currentPage} {pageAmount} />

{#if !err && !pageOptions.isPublic}
    {#if pageOptions.type === "member"}
    <NewMember on:create={addItemToList} />
    {:else if pageOptions.type === "group"}
    <NewGroup on:create={addItemToList} />
    {/if}
{/if}
{#if pageOptions.view === "card"}
    <CardView on:update={update} {otherList} {pageOptions} {lists} />
{:else if pageOptions.view === "tiny"}
    tiny!
{:else}
<ListView on:update={update} on:deletion={updateDelete} {otherList} {lists} {pageOptions} {options} />
{/if}
<ListPagination bind:currentPage={pageOptions.currentPage} {pageAmount} />
{/if}

<style>
    .itemcounter {
        width: 100%;
        text-align: center;
        display: inline-block;
        margin-bottom: 0.5rem;
    }
</style>