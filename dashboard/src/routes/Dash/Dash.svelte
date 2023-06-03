<script lang="ts">
    import { Container, Col, Row, TabContent, TabPane } from 'sveltestrap';
    import { navigate, useLocation } from "svelte-navigator";
    import { currentUser, loggedIn } from '../../stores';
    
    import SystemMain from '../../components/system/Main.svelte';
    import MemberList from '../../components/list/MemberList.svelte';
    import GroupList from '../../components/list/GroupList.svelte';

    import type { System, Member, Group } from '../../api/types';
    import api from '../../api';
    import { defaultListOptions, defaultPageOptions, type List as Lists, type ListOptions, type PageOptions } from '../../components/list/types';
    import { setContext } from 'svelte';
    import { writable } from 'svelte/store';

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
    
    // context stores for each list
    let memberStore = writable<Member[]>([]);
    let groupStore = writable<Group[]>([]);

    // state handling
    let errs: Record<string, string> = {};
    let loading: Record<string, boolean> = {};
    
    setContext("members", memberStore);
    setContext("groups", groupStore);

    fetchLists()

    // fetch both lists, and store them inside a context store
    async function fetchLists() {
        loading.members = true;
        loading.groups = true;

        try {
            const res = await api().systems("@me").members.get({ auth: true });
            memberStore.set(res)
            loading.members = false;

        } catch (error) {
            console.error(error);
            errs.members = error.message;
        }

        try {
            const res = await api().systems("@me").groups.get({ auth: true, query: { with_members: true } });
            groupStore.set(res)
            loading.groups = false;

        } catch (error) {
            console.error(error);
            errs.groups = error.message;
        }
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
                    <MemberList on:viewChange={(e) => navigateTo("members", e.detail)} bind:listLoading={loading.members} pageOptions={memberListPageOptions} options={memberListOptions} />
                </TabPane>
                <TabPane tabId="groups" tab="Groups" active={tabPane === "groups"}>
                    <GroupList on:viewChange={(e) => navigateTo("groups", e.detail)} bind:listLoading={loading.groups} pageOptions={groupListPageOptions}  options={groupListOptions} />
                </TabPane> 
            </TabContent>
        </Col>
    </Row>
</Container>
{/if}

<svelte:head>
    <title>PluralKit | dash</title>
</svelte:head>