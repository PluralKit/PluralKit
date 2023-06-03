<script lang="ts">
    import { Container, Col, Row, TabContent, TabPane, Alert, Spinner } from 'sveltestrap';
    import { useParams, useLocation, navigate } from "svelte-navigator";
    import { onMount, setContext } from 'svelte';
    
    import SystemMain from '../../components/system/Main.svelte';
    import MemberList from '../../components/list/MemberList.svelte';
    import GroupList from '../../components/list/GroupList.svelte';
    import { defaultListOptions, defaultPageOptions, type List as Lists, type ListOptions, type PageOptions } from '../../components/list/types';
    
    import type{ Group, Member, System } from '../../api/types';
    import api from '../../api';
    import { writable } from 'svelte/store';

    let user: System = {};
    let settings = JSON.parse(localStorage.getItem("pk-settings"));

    let params = useParams();
    $: systemId = $params.id;
    
    let location = useLocation();
    let urlParams = $location.search && new URLSearchParams($location.search);
    
    let tabPane: string|number = urlParams && urlParams.get("tab") || "system";
    let listView: string = urlParams && urlParams.get("view") || "list";

    // change the URL when changing tabs
    function navigateTo(tab: string|number, view: string) {
        let url = `./${systemId}`;
        if (tab || view) url += "?";
        if (tab) url += `tab=${tab}`
        if (tab && view) url += "&";
        if (view) url += `view=${view}`

        navigate(url);
        tabPane = tab;
    }
    
    let err: string;
    
    let title = "system"

    onMount(() => {
        getSystem();
        fetchLists();
    })

    async function getSystem() {
        try {
            let res: System = await api().systems(systemId).get();
            user = res;
            title = user.name ? user.name : "system";
        } catch (error) {
            console.log(error);
            err = error.message;
        }
    }

    // context stores for each list
    let memberStore = writable<Member[]>([]);
    let groupStore = writable<Group[]>([]);

    // state handling
    let errs: Record<string, string> = {};
    let loading: Record<string, boolean> = {};
    
    setContext("members", memberStore);
    setContext("groups", groupStore);

    // fetch both lists, and store them inside a context store
    async function fetchLists() {
        loading.members = true;
        loading.groups = true;

        try {
            const res = await api().systems(systemId).members.get();
            memberStore.set(res)
            loading.members = false;

        } catch (error) {
            console.error(error);
            errs.members = error.message;
        }

        try {
            const res = await api().systems(systemId).groups.get();
            groupStore.set(res)
            loading.groups = false;

        } catch (error) {
            console.error(error);
            errs.groups = error.message;
        }
    }

    let groupListOptions: ListOptions = defaultListOptions;
    let memberListOptions: ListOptions = defaultListOptions;
    
    let pageOptions: PageOptions = defaultPageOptions;
    let memberListPageOptions: PageOptions = {...pageOptions, ...{
        view: listView,
        isPublic: true,
        type: 'member'
    }};

    let groupListPageOptions: PageOptions = {...pageOptions, ...{
        view: listView,
        isPublic: true,
        type: 'group'
    }};

</script>

<!-- display the banner if there's a banner set, and if the current settings allow for it-->
{#if user && user.banner && ((settings && settings.appearance.banner_top) || !settings)}
<div class="banner" style="background-image: url({user.banner})" />
{/if}
<Container>
    <Row>
        <h1 class="visually-hidden">Viewing a public system</h1>
        <Col class="mx-auto" xs={12} lg={11} xl={10}>
            {#if !user.id && !err}
            <div class="mx-auto text-center">
                <Spinner class="d-inline-block" />
            </div>
            {:else if err}
                <Alert color="danger">{err}</Alert>
            {:else}
            <Alert color="info" aria-hidden>You are currently <b>viewing</b> a system.</Alert>
            <TabContent class="mt-3" on:tab={(e) => navigateTo(e.detail, listView)}>
                <TabPane tabId="system" tab="System" active={tabPane === "system"}>
                    <SystemMain bind:user={user} isPublic={true} />
                </TabPane>
                <TabPane tabId="members" tab="Members" active={tabPane === "members"}>
                    <MemberList on:viewChange={(e) => navigateTo("members", e.detail)} bind:listLoading={loading.members} pageOptions={memberListPageOptions} options={memberListOptions} {systemId} />
                </TabPane>
                <TabPane tabId="groups" tab="Groups" active={tabPane === "groups"}>
                    <GroupList on:viewChange={(e) => navigateTo("groups", e.detail)} bind:listLoading={loading.groups} pageOptions={groupListPageOptions}  options={groupListOptions} {systemId} />
                </TabPane> 
            </TabContent>
            {/if}
        </Col>
    </Row>
</Container>

<svelte:head>
    <title>PluralKit | {title}</title>
</svelte:head>