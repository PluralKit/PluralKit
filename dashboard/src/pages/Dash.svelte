<script lang="ts">
    import { Container, Col, Row, TabContent, TabPane } from 'sveltestrap';
    import { navigate, useLocation } from "svelte-navigator";
    import { currentUser, loggedIn } from '../stores';
    
    import SystemMain from '../lib/system/Main.svelte';
    import MemberList from '../lib/member/List.svelte';
    import GroupList from '../lib/group/List.svelte';

    import { System } from '../api/types';
    import api from '../api';

    let isPublic = false;

    // get the state from the navigator so that we know which tab to start on
    let location = useLocation();
    let params = $location.search && new URLSearchParams($location.search);
    let tabPane: string;
    if (params) {
        tabPane = params.get("tab");
    }
    
    if (!tabPane) {
        tabPane = "system";
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
            localStorage.removeItem("pk-token");
            localStorage.removeItem("pk-user");
            currentUser.update(() => null);
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
<Container>
    <Row>
        <Col class="mx-auto" xs={12} lg={11} xl={10}>
            <TabContent class="mt-3">
                <TabPane tabId="system" tab="System" active={tabPane === "system"}>
                        <SystemMain bind:user={user} bind:isPublic />
                </TabPane>
                <TabPane tabId="members" tab="Members" active={tabPane === "members"}>
                        <MemberList bind:groups={groups} bind:list={members} bind:isPublic />
                </TabPane>
                <TabPane tabId="groups" tab="Groups" active={tabPane === "groups"}>
                    <GroupList bind:members={members} bind:list={groups} bind:isPublic />
            </TabPane> 
            </TabContent>
        </Col>
    </Row>
</Container>

<svelte:head>
    <title>pk-webs | dash</title>
</svelte:head>