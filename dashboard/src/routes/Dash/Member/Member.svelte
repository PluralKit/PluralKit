<script lang="ts">
    import { Container, Row, Col, Alert, Spinner, Card, CardHeader, CardBody, CardTitle, Tooltip } from "sveltestrap";
    import Body from '../../../components/member/Body.svelte';
    import ListView from '../../../components/list/ListView.svelte';
    import { useParams, Link, navigate, useLocation } from 'svelte-navigator';
    import { onMount } from 'svelte';
    import api from "../../../api";
    import type { Member, Group } from "../../../api/types";
    import CardsHeader from "../../../components/common/CardsHeader.svelte";
    import FaAddressCard from 'svelte-icons/fa/FaAddressCard.svelte'
    import ListPagination from '../../../components/common/ListPagination.svelte';
    import CardView from '../../../components/list/CardView.svelte';
    import type { List as Lists, ListOptions, PageOptions } from '../../../components/list/types';
    import { defaultListOptions, defaultPageOptions } from '../../../components/list/types';
    import { filterList, getPageAmount, paginateList } from '../../../components/list/functions';
    import PageControl from "../../../components/list/PageControl.svelte";

    // get the state from the navigator so that we know which tab to start on
    let location = useLocation();
    let urlParams = $location.search && new URLSearchParams($location.search);
    
    let listView: string = urlParams && urlParams.get("view") || "list";

    let loading = true;
    let groupLoading = false;
    let params = useParams();
    let err = "";
    let groupErr = "";
    let member: Member;
    let systemGroups: Group[] = [];
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
        fetchMember();
    });

    let title = isPublic ? "member" : "member (dash)";

    async function fetchMember() {
        try {
            member = await api().members($params.id).get({auth: !isPublic});
            if (!isPublic && !member.privacy) {
                notOwnSystem = true;
                throw new Error("Member is not from own system.");
            }
            err = "";
            loading = false;
            if (member.name) {
                title = isPublic ? member.name : `${member.name} (dash)`;
            }
            groupLoading = true;
            fetchGroups();
        } catch (error) {
            console.log(error);
            err = error.message;
            loading = false;
        }
    }

    async function fetchGroups() {
        try {
            lists.rawList = await api().members($params.id).groups().get({auth: !isPublic, query: { with_members: !isPublic } });
            if (!isPublic) {
                systemGroups = await api().systems("@me").groups.get({ auth: true, query: { with_members: true } });
            }
            groupErr = "";
            groupLoading = false;
        } catch (error) {
            console.log(error);
            groupErr = error.message;
            groupLoading = false;
        }
    }

    let lists: Lists<Group> = {
        rawList: [],
        processedList: [],
        currentPage: [],

        shortGroups: [],
        shortMembers: [],
    }

    let nope: Lists<Member> = {
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
        type: 'group',
        isMain: false,
        itemsPerPage: listView === 'card' ? 24 : 25
    };

    async function copyShortLink(event?) {
        if (event) {
            let ctrlDown = event.ctrlKey||event.metaKey; // mac support
            if (!(ctrlDown && event.key === "c") && event.key !== "Enter") return;
        }
        try {
            await navigator.clipboard.writeText(`https://pk.mt/m/${member.id}`);
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
    <div class="background" style="background-color: {member && `#${member.color}`}"></div>
{/if}
{#if member && member.banner && settings && settings.appearance.banner_top && !notOwnSystem}
<div class="banner" style="background-image: url({member.banner})" />
{/if}
<Container>
    <Row>
        <Col class="mx-auto" xs={12} lg={11} xl={10}>
            <h2 class="visually-hidden">Viewing {isPublic ? "a public" : "your own"} member</h2>
            {#if isDeleted}
                <Alert color="success">Member has been successfully deleted. <Link to="/dash">Return to dash</Link></Alert>
            {:else}
            {#if isPublic}
                <Alert color="info" aria-hidden>You are currently <b>viewing</b> a member.</Alert>
            {/if}
            {#if notOwnSystem}
                <Alert color="danger">This member does not belong to your system, did you mean to look up <Link to={`/profile/m/${member.id}`}>their public page</Link>?</Alert>
            {:else if err}
                <Alert color="danger">{@html err}</Alert>
            {:else if loading}
                <Spinner/>
            {:else if member && member.id}
                <Card class="mb-4">
                    <CardHeader>
                        <CardsHeader bind:item={member}>
                            <div slot="icon" style="cursor: pointer;" id={`member-copy-${member.id}`} on:click|stopPropagation={() => copyShortLink()} on:keydown={(e) => copyShortLink(e)} tabindex={0} >
                                <FaAddressCard slot="icon" />
                            </div>
                        </CardsHeader>
                        <Tooltip placement="top" target={`member-copy-${member.id}`}>{copied ? "Copied!" : "Copy public link"}</Tooltip>
                    </CardHeader>
                    <CardBody>
                        <Body bind:groups={systemGroups} bind:member={member} isPage={true} isPublic={isPublic}/>
                    </CardBody>
                </Card>
            {/if}
            {#if groupLoading}
                <Alert color="primary"><Spinner size="sm" /> Fetching groups...</Alert>
            {:else if groupErr}
                <Alert color="danger">{groupErr}</Alert>
            {:else if lists.rawList && lists.rawList.length > 0}
            <PageControl bind:options={listOptions} bind:pageOptions />
            <span class="itemcounter">{lists.processedList.length} {pageOptions.type}s ({lists.currentPage.length} shown)</span>
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
    
    .itemcounter {
        width: 100%;
        text-align: center;
        display: inline-block;
        margin-bottom: 0.5rem;
    }
</style>

<svelte:head>
    <title>PluralKit | {title}</title>
</svelte:head>