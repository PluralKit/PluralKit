<script lang="ts">
    import { Container, Col, Row, TabContent, TabPane, Alert, Spinner } from 'sveltestrap';
    import { useParams, useLocation, navigate } from "svelte-navigator";
    import { onMount } from 'svelte';
    
    import SystemMain from '../../components/system/Main.svelte';
    import List from '../../components/list/List.svelte';
    import { defaultListOptions, defaultPageOptions, type List as Lists, type ListOptions, type PageOptions } from '../../components/list/types';
    
    import type{ Group, Member, System } from '../../api/types';
    import api from '../../api';

    let user: System = {};
    let settings = JSON.parse(localStorage.getItem("pk-settings"));

    let params = useParams();
    $: id = $params.id;
    
    let location = useLocation();
    let urlParams = $location.search && new URLSearchParams($location.search);
    
    let tabPane: string|number = urlParams && urlParams.get("tab") || "system";
    let listView: string = urlParams && urlParams.get("view") || "list";

    // change the URL when changing tabs
    function navigateTo(tab: string|number, view: string) {
        let url = `./${id}`;
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
    })

    async function getSystem() {
        try {
            let res: System = await api().systems(id).get();
            user = res;
            title = user.name ? user.name : "system";
        } catch (error) {
            console.log(error);
            err = error.message;
        }
    }

    let memberList: Lists<Member> = {
        rawList: [],
        processedList: [],
        currentPage: [],

        shortGroups: [],
        shortMembers: [],
    }

    let groupList: Lists<Group> = {
        rawList: [],
        processedList: [],
        currentPage: [],

        shortGroups: [],
        shortMembers: [],
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
                    <List on:viewChange={(e) => navigateTo("members", e.detail)} bind:otherList={groupList} bind:lists={memberList} bind:pageOptions={memberListPageOptions} bind:options={memberListOptions} />
                </TabPane>
                <TabPane tabId="groups" tab="Groups" active={tabPane === "groups"}>
                    <List on:viewChange={(e) => navigateTo("members", e.detail)} bind:otherList={memberList} bind:lists={groupList} bind:pageOptions={groupListPageOptions}  bind:options={groupListOptions} />
                </TabPane> 
            </TabContent>
            {/if}
        </Col>
    </Row>
</Container>

<svelte:head>
    <title>PluralKit | {title}</title>
</svelte:head>