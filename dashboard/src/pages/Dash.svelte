<script lang="ts">
    import { Container, Col, Row, TabContent, TabPane } from 'sveltestrap';
    import { navigate, useLocation } from "svelte-navigator";
    import { currentUser, loggedIn } from '../stores';
    
    import SystemMain from '../lib/system/Main.svelte';
    import List from '../lib/list/List.svelte';

    import { System } from '../api/types';
    import api from '../api';

    // get the state from the navigator so that we know which tab to start on
    let location = useLocation();
    let params = $location.search && new URLSearchParams($location.search);
    let tabPane: string|number;
    if (params) {
        tabPane = params.get("tab");
    }
    
    if (!tabPane) {
        tabPane = "system";
    }

    

    // change the URL when changing tabs
    function navigateTo(tab: string|number) {
        navigate(`./dash?tab=${tab}`);
        tabPane = tab;
    }

    // subscribe to the cached user in the store
    let current;
    currentUser.subscribe(value => {
        current = value;
    });
    
    // if there is no cached user, get the user from localstorage
    let user: System = current ?? JSON.parse(localStorage.getItem("pk-user"));
    // since the user in localstorage can be outdated, fetch the user from the api again
    if (!current) {
        login(localStorage.getItem("pk-token"));
    }

    // if there's no user, and there's no token, assume the login failed and send us back to the homepage.
    if (!localStorage.getItem("pk-token") && !user) {
        navigate("/");
    }

    let settings = JSON.parse(localStorage.getItem("pk-settings"));

    // just the login function
    async function login(token: string) {
        try {
            if (!token) {
                throw new Error("Token cannot be empty.")
            }
            const res: System = await api().systems("@me").get({ token });
            localStorage.setItem("pk-token", token);
            localStorage.setItem("pk-user", JSON.stringify(res));
            loggedIn.update(() => true);
            currentUser.update(() => res);
            user = res;
        } catch (error) {
            console.log(error);
            // localStorage.removeItem("pk-token");
            // localStorage.removeItem("pk-user");
            // currentUser.update(() => null);
            navigate("/");
        }
    }
    
    // some values that get passed from the member to the group components and vice versa
    let members = [];
    let groups = [];

</script>

<!-- display the banner if there's a banner set, and if the current settings allow for it-->
{#if user && user.banner && ((settings && settings.appearance.banner_top) || !settings)}
<div class="banner" style="background-image: url({user.banner})" />
{/if}
{#if user}
<Container>
    <Row>
        <Col class="mx-auto" xs={12} lg={11} xl={10}>
            <h2 class="visually-hidden">Viewing your own system</h2>
            <TabContent class="mt-3" on:tab={(e) => navigateTo(e.detail)}>
                <TabPane tabId="system" tab="System" active={tabPane === "system"}>
                        <SystemMain bind:user={user} isPublic={false} />
                </TabPane>
                <TabPane tabId="members" tab="Members" active={tabPane === "members"}>
                        <List bind:groups={groups} bind:members={members} isPublic={false} itemType={"member"}/>
                </TabPane>
                <TabPane tabId="groups" tab="Groups" active={tabPane === "groups"}>
                    <List bind:members={members} bind:groups={groups} isPublic={false} itemType={"group"}/>
            </TabPane> 
            </TabContent>
        </Col>
    </Row>
</Container>
{/if}

<svelte:head>
    <title>PluralKit | dash</title>
</svelte:head>