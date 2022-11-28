<script lang="ts">
    import { Container, Col, Row, TabContent, TabPane } from 'sveltestrap';
    import { navigate, useLocation } from "svelte-navigator";
    import { currentUser, loggedIn } from '../../stores';
    
    import SystemMain from '../../components/system/Main.svelte';
    import List from '../../components/list/List.svelte';

    import type { System, Member, Group } from '../../api/types';
    import api from '../../api';
    import { defaultListOptions, defaultPageOptions, type List as Lists, type ListOptions, type PageOptions } from '../../components/list/types';

    // get the state from the navigator so that we know which tab to start on
    let location = useLocation();
    let params = $location.search && new URLSearchParams($location.search);
    
    let tabPane: string|number = params && params.get("tab") || "system";
    let listView: string = params && params.get("view") || "list";

    // change the URL when changing tabs
    function navigateTo(tab: string|number, view: string) {
        let url = "./dash";
        if (tab || view) url += "?";
        if (tab) url += `tab=${tab}`
        if (tab && view) url += "&";
        if (view) url += `view=${view}`

        navigate(url);
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

    let groupListOptions: ListOptions = JSON.parse(JSON.stringify(defaultListOptions));
    let memberListOptions: ListOptions = JSON.parse(JSON.stringify(defaultListOptions));
    
    let memberListPageOptions: PageOptions = {...defaultPageOptions,
        view: listView,
        isPublic: false,
        type: 'member'
    };

    let groupListPageOptions: PageOptions = {...defaultPageOptions,
        view: listView,
        isPublic: false,
        type: 'group'
    };

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
            <TabContent class="mt-3" on:tab={(e) => navigateTo(e.detail, e.detail === 'members' ? memberListPageOptions.view : e.detail === 'groups' ? groupListPageOptions.view : 'list')}>
                <TabPane tabId="system" tab="System" active={tabPane === "system"}>
                        <SystemMain bind:user={user} isPublic={false} />
                </TabPane>
                <TabPane tabId="members" tab="Members" active={tabPane === "members"}>
                    <List on:viewChange={(e) => navigateTo("members", e.detail)} bind:otherList={groupList} bind:lists={memberList} bind:pageOptions={memberListPageOptions} bind:options={memberListOptions} />
                </TabPane>
                <TabPane tabId="groups" tab="Groups" active={tabPane === "groups"}>
                    <List on:viewChange={(e) => navigateTo("groups", e.detail)} bind:otherList={memberList} bind:lists={groupList} bind:pageOptions={groupListPageOptions}  bind:options={groupListOptions} />
                </TabPane> 
            </TabContent>
        </Col>
    </Row>
</Container>
{/if}

<svelte:head>
    <title>PluralKit | dash</title>
</svelte:head>