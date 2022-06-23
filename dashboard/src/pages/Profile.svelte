<script lang="ts">
    import { Container, Col, Row, TabContent, TabPane, Alert, Spinner } from 'sveltestrap';
    import { useParams, useLocation, navigate } from "svelte-navigator";
    import { onMount } from 'svelte';
    
    import SystemMain from '../lib/system/Main.svelte';
    import List from '../lib/list/List.svelte';

    import { System } from '../api/types';
    import api from '../api';

    let user: System = {};
    let settings = JSON.parse(localStorage.getItem("pk-settings"));

    let members = [];
    let groups = [];

    let params = useParams();
    $: id = $params.id;
    
    let location = useLocation();
    let urlParams = $location.search && new URLSearchParams($location.search);
    let tabPane: string;
    if (urlParams) {
        tabPane = urlParams.get("tab");
    }
    
    if (!tabPane) {
        tabPane = "system";
    }

    function navigateTo(tab: string|number) {
        navigate(`./${id}?tab=${tab}`)
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
            <TabContent class="mt-3" on:tab={(e) => navigateTo(e.detail)}>
                <TabPane tabId="system" tab="System" active={tabPane === "system"}>
                        <SystemMain bind:user isPublic={true} />
                </TabPane>
                <TabPane tabId="members" tab="Members" active={tabPane === "members"}>
                        <List members={members} groups={groups} isPublic={true} itemType={"member"} />
                </TabPane>
                <TabPane tabId="groups" tab="Groups" active={tabPane === "groups"}>
                    <List members={members} groups={groups} isPublic={true} itemType={"group"} />
            </TabPane> 
            </TabContent>
            {/if}
        </Col>
    </Row>
</Container>

<svelte:head>
    <title>PluralKit | {title}</title>
</svelte:head>