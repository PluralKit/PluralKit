<script lang="ts">
    import { Container, Row, Col, Alert, Spinner, Card, CardHeader, CardBody, CardTitle, Tooltip } from "sveltestrap";
    import Body from '../../../components/group/Body.svelte';
    import { useParams, Link, navigate, useLocation } from 'svelte-navigator';
    import { onMount } from 'svelte';
    import api from "../../../api";
    import type { Member, Group } from "../../../api/types";
    import CardsHeader from "../../../components/common/CardsHeader.svelte";
    import FaUsers from 'svelte-icons/fa/FaUsers.svelte';
    import ListPagination from '../../../components/common/ListPagination.svelte';
    import ListView from '../../../components/list/ListView.svelte';
    import CardView from '../../../components/list/CardView.svelte';
    import type { List as Lists, ListOptions, PageOptions } from '../../../components/list/types';
    import { defaultListOptions, defaultPageOptions } from '../../../components/list/types';
    import { filterList, paginateList, getPageAmount } from '../../../components/list/functions';
    import PageControl from "../../../components/list/PageControl.svelte";

    // get the state from the navigator so that we know which tab to start on
    let location = useLocation();
    let urlParams = $location.search && new URLSearchParams($location.search);
    
    let listView: string = urlParams && urlParams.get("view") || "list";

    let loading = true;
    let memberLoading = false;
    let params = useParams();
    let err = "";
    let memberErr = "";
    let group: Group;
    let systemMembers: Group[] = [];
    let isDeleted = false;
    let notOwnSystem = false;
    let copied = false;
    let pageAmount = 1;

    export let isPublic = true;
    let settings = JSON.parse(localStorage.getItem("pk-settings"));

    if (!isPublic) {
        let user = localStorage.getItem("pk-user");
        if (!user) navigate("/");
    }

    onMount(() => {
        fetchGroup();
    });

    let title = isPublic ? "group" : "group (dash)";

    async function fetchGroup() {
        try {
            group = await api().groups($params.id).get({auth: !isPublic});
            if (!isPublic && !group.privacy) {
                notOwnSystem = true;
                throw new Error("Group is not from own system.");
            }
            err = "";
            loading = false;
            if (group.name) {
                title = isPublic ? group.name : `${group.name} (dash)`;
            }
            memberLoading = true;
            fetchMembers();
        } catch (error) {
            console.log(error);
            err = error.message;
            loading = false;
        }
    }

    async function fetchMembers() {
        try {
            lists.rawList = await api().groups($params.id).members().get({auth: !isPublic});
            group.members = lists.rawList.map(function(member) {return member.uuid});
            if (!isPublic) {
                systemMembers = await api().systems("@me").members.get({ auth: true });
            }
            memberErr = "";
            memberLoading = false;
        } catch (error) {
            console.log(error);
            memberErr = error.message;
            memberLoading = false;
        }
    }

    let lists: Lists<Member> = {
        rawList: [],
        processedList: [],
        currentPage: [],

        shortGroups: [],
        shortMembers: [],
    }

    let nope: Lists<Group> = {
        rawList: [],
        processedList: [],
        currentPage: [],

        shortGroups: [],
        shortMembers: [],
    }

    let listOptions: ListOptions = {...defaultListOptions};
    
    let pageOptions: PageOptions = {...defaultPageOptions,
        view: listView,
        isPublic: isPublic,
        type: 'member',
        isMain: false,
        itemsPerPage: listView === 'card' ? 24 : 25
    };

    async function copyShortLink(event?) {
        if (event) {
            let ctrlDown = event.ctrlKey||event.metaKey; // mac support
            if (!(ctrlDown && event.key === "c") && event.key !== "Enter") return;
        }
        try {
            await navigator.clipboard.writeText(`https://pk.mt/g/${group.id}`);
            copied = true;
            await new Promise(resolve => setTimeout(resolve, 2000));
            copied = false;
        } catch (error) {
            console.log(error);
        }
    }

    $: lists.processedList = filterList(lists.rawList, listOptions);
    $: lists.currentPage = paginateList(lists.processedList, pageOptions);
    $: pageAmount = getPageAmount(lists.processedList, pageOptions);

    
    function updateDelete(event: any) {
        lists.rawList = lists.rawList.filter(m => m.id !== event.detail);
    }

    function update(event: any) {
        lists.rawList = lists.rawList.map(m => m.id === event.detail.id ? m = event.detail : m);
    }
</script>

{#if settings && settings.appearance.color_background && !notOwnSystem}
    <div class="background" style="background-color: {group && `#${group.color}`}"></div>
{/if}
{#if group && group.banner && settings && settings.appearance.banner_top && !notOwnSystem}
<div class="banner" style="background-image: url({group.banner})" />
{/if}
<Container>
    <Row>
        <Col class="mx-auto" xs={12} lg={11} xl={10}>
            <h2 class="visually-hidden">Viewing {isPublic ? "a public" : "your own"} group</h2>
            {#if isDeleted}
                <Alert color="success">Group has been successfully deleted. <Link to="/dash">Return to dash</Link></Alert>
            {:else}
            {#if isPublic}
                <Alert color="info" aria-hidden>You are currently <b>viewing</b> a group.</Alert>
            {/if}
            {#if notOwnSystem}
                <Alert color="danger">This group does not belong to your system, did you mean to look up <Link to={`/profile/g/${group.id}`}>it's public page</Link>?</Alert>
            {:else if err}
                <Alert color="danger">{@html err}</Alert>
            {:else if loading}
                <Spinner/>
            {:else if group && group.id}
                <Card class="mb-4">
                    <CardHeader>
                        <CardsHeader bind:item={group}>
                            <div slot="icon" style="cursor: pointer;" id={`group-copy-${group.id}`} on:click|stopPropagation={() => copyShortLink()} on:keydown={(e) => copyShortLink(e)} tabindex={0} >
                                <FaUsers slot="icon" />
                            </div>
                        </CardsHeader>
                        <Tooltip placement="top" target={`group-copy-${group.id}`}>{copied ? "Copied!" : "Copy public link"}</Tooltip>
                    </CardHeader>
                    <CardBody>
                        <Body bind:members={systemMembers} bind:group={group} isPage={true} isPublic={isPublic}/>
                    </CardBody>
                </Card>
            {/if}
            {#if memberLoading}
                <Alert color="primary"><Spinner size="sm" /> Fetching members...</Alert>
            {:else if memberErr}
                <Alert color="danger">{memberErr}</Alert>
            {:else if lists.rawList && lists.rawList.length > 0}
            <PageControl bind:options={listOptions} bind:pageOptions />
                <ListPagination bind:currentPage={pageOptions.currentPage} {pageAmount} />
                {#if pageOptions.view === "card"}
                <CardView {pageOptions} {lists} otherList={nope} on:update={update} />
                {:else}
                <ListView {pageOptions} {lists} otherList={nope} on:update={update} on:deletion={updateDelete} />
                {/if}
                <ListPagination bind:currentPage={pageOptions.currentPage} {pageAmount} />
            {/if}
            {/if}
        </Col>
    </Row>
</Container>

<style>
    .background {
        position: fixed;
        top: 0;
        left: 0;
        width: 100%;
        flex: 1;
        min-height: 100%;
        z-index: -30;
    }
</style>

<svelte:head>
    <title>PluralKit | {title}</title>
</svelte:head>

