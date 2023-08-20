<script lang="ts">
    import { Alert, Row, Col, Spinner, Button } from 'sveltestrap';
    import type { Writable } from 'svelte/store';
    import { getContext } from 'svelte';

    import NewGroup from '../group/NewGroup.svelte';
    import ListPagination from '../common/ListPagination.svelte';
    import ListControl from './ListControl.svelte';
    import ListView from './ListView.svelte';
    import CardView from './CardView.svelte';

    import type { Member, Group } from '../../api/types';
    import api from '../../api';
    import type { ListOptions, PageOptions } from './types';
    import { createShortList, filterList, getPageAmount, paginateList } from './functions';
    import TinyView from './TinyView.svelte';
    import TextView from './TextView.svelte';

    $: memberList = getContext<Writable<Member[]>>("members")
    $: groupList = getContext<Writable<Group[]>>("groups")

    $: members = $memberList || []
    $: groups = $groupList || []

    export let options: ListOptions;
    export let pageOptions: PageOptions;
    export let systemId: string = "";

    // general state handling
    export let listLoading = true;
    export let err: string = "";

    let settings = JSON.parse(localStorage.getItem("pk-settings"));
    
    // set the default page amount to 1 so we don't get divisions by 0
    let pageAmount = 1;

    // set the default items per page based on settings and view
    // this probably should be moved to it's own function
    if (pageOptions.view === "card") pageOptions.itemsPerPage = 24;
    else if (pageOptions.view === "tiny") pageOptions.itemsPerPage = 36;
    else if (settings && settings.accessibility && settings.accessibility.expandedcards) pageOptions.itemsPerPage = 10;
    else pageOptions.itemsPerPage = 25;

    async function fetchList() {
        err = "";
        listLoading = true;
        try {
            const res: Group[] = await api().systems(pageOptions.isPublic ? systemId : "@me").groups.get({ auth: !pageOptions.isPublic, query: { with_members: !pageOptions.isPublic } });
            groupList.set(res)
        } catch (error) {
            console.log(error);
            err = error.message;
        }
        listLoading = false;
    }

    $: processedGroups = filterList(groups, groups, options, "group")
    $: currentGroups = paginateList(processedGroups, pageOptions)
    $: shortMembers = createShortList(members)
    $: pageAmount = getPageAmount(processedGroups, pageOptions)
    $: shortGroups = createShortList(groups)
    
</script>

<ListControl on:viewChange bind:options bind:pageOptions {shortGroups} {shortMembers} />

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
<span class="itemcounter">{processedGroups.length} {pageOptions.type}s ({currentGroups.length} shown) <a href="#!" on:click={(e) => {e.preventDefault(); fetchList()}}>Refresh list</a></span>

<ListPagination bind:currentPage={pageOptions.currentPage} {pageAmount} />

{#if !err && !pageOptions.isPublic}
    <NewGroup />
{/if}
{#if pageOptions.view === "card"}
    <CardView {pageOptions} currentList={currentGroups} />
{:else if pageOptions.view === "tiny"}
    <TinyView {pageOptions} currentList={currentGroups} />
{:else if pageOptions.view === "text"}
    <TextView {pageOptions} listOptions={options} currentList={currentGroups} />
{:else}
<ListView currentList={currentGroups} {pageOptions} {options} fullListLength={groups.length} />
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