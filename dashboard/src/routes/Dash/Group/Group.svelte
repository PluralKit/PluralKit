<script lang="ts">
    import { Container, Row, Col, Alert, Spinner, Card, CardHeader, CardBody, CardTitle, Tooltip } from "sveltestrap";
    import Body from '../../../components/group/Body.svelte';
    import { useParams, Link, navigate, useLocation } from 'svelte-navigator';
    import { onMount, setContext } from 'svelte';
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
    import { writable, type Writable } from "svelte/store";

    // get the state from the navigator so that we know which tab to start on
    let location = useLocation();
    let urlParams = $location.search && new URLSearchParams($location.search);
    
    let listView: string = urlParams && urlParams.get("view") || "list";

    let loading = true;
    let memberLoading = false;
    let params = useParams();
    let err = "";
    let memberErr = "";
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

    onMount(async () => {
        await fetchGroup();
    });

    let membersStore: Writable<Member[]> = writable([])
    let groupsStore: Writable<Group[]> = writable([])
    $: members = setContext<Writable<Member[]>>("members", membersStore)
    $: groups = setContext<Writable<Group[]>>("groups", groupsStore)
    $: group = $groups.filter(g => g.id === $params.id)[0] || {}

    let title = isPublic ? "group" : "group (dash)";

    async function fetchGroup() {
        try {
            const res = await api().groups($params.id).get({auth: !isPublic});
            $groups = [res]
            group = $groups.filter(g => g.id === $params.id)[0];
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
            if (isPublic) {
                const groupMembers: Member[] = await api().groups($params.id).members().get({auth: !isPublic});
                group.members = groupMembers.map((m: Member) => m.uuid);
                members.set(groupMembers);
            } else {
                const systemGroups: Group[] = await api().systems("@me").groups.get({ auth: true, query: { with_members: true } });
                group.members = systemGroups.filter((g: Group) => g.id === $params.id).map((m: Member) => m.uuid);
                groups.set(systemGroups);

                const systemMembers = await api().systems("@me").members.get({ auth: true });
                members.set(systemMembers);
            }
            memberErr = "";
            memberLoading = false;
        } catch (error) {
            console.log(error);
            memberErr = error.message;
            memberLoading = false;
        }
    }

    let listOptions: ListOptions = {...defaultListOptions};
    
    let pageOptions: PageOptions = {...defaultPageOptions,
        view: listView,
        isPublic: isPublic,
        type: 'member',
        isMain: false,
        itemsPerPage: getDefaultItemsPerpage()
    };

    function getDefaultItemsPerpage(): number {
        if (listView === 'card') return 24;
        else if (settings && settings.accessibility && settings.accessibility.expandedcards) 
            return 10;
        else return 25;
    }

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

    $: groupMembers = $members.filter(m => group.members.includes(m.uuid));
    $: processedList = filterList(groupMembers, $groups, listOptions);
    $: currentPage = paginateList(processedList, pageOptions);
    $: pageAmount = getPageAmount(processedList, pageOptions);

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
                        <CardsHeader item={group}>
                            <div slot="icon" style="cursor: pointer;" id={`group-copy-${group.id}`} on:click|stopPropagation={() => copyShortLink()} on:keydown={(e) => copyShortLink(e)} tabindex={0} >
                                <FaUsers slot="icon" />
                            </div>
                        </CardsHeader>
                        <Tooltip placement="top" target={`group-copy-${group.id}`}>{copied ? "Copied!" : "Copy public link"}</Tooltip>
                    </CardHeader>
                    <CardBody>
                        <Body {group} isPage={true} isPublic={isPublic}/>
                    </CardBody>
                </Card>
            {/if}
            {#if memberLoading}
                <Alert color="primary"><Spinner size="sm" /> Fetching members...</Alert>
            {:else if memberErr}
                <Alert color="danger">{memberErr}</Alert>
            {:else if groupMembers && groupMembers.length > 0}
            <PageControl bind:options={listOptions} bind:pageOptions />
            <span class="itemcounter">{processedList.length} {pageOptions.type}s ({currentPage.length} shown)</span>
                <ListPagination bind:currentPage={pageOptions.currentPage} {pageAmount} />
                {#if pageOptions.view === "card"}
                <CardView {pageOptions} currentList={currentPage} />
                {:else}
                <ListView {pageOptions} currentList={currentPage} fullListLength={groupMembers.length}/>
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

