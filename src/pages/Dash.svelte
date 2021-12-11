<script lang="ts">
    import { Container, Col, Row, TabContent, TabPane, Card } from 'sveltestrap';
    import { navigate, useLocation } from "svelte-navigator";
    import { currentUser, loggedIn } from '../stores';
    
    import PrivateSystem from '../lib/cards/System.svelte';
    import PKAPI from '../api';
    import type Sys from '../api/system';

    let location = useLocation();

    let tabPane = $location.state && $location.state.tab;

    if (tabPane === undefined) {
        tabPane = "system";
    }

    let current;
    currentUser.subscribe(value => {
        current = value;
    });

    if (!current) {
        login(localStorage.getItem("pk-token"));
    }

    let user = current !== null ? current : JSON.parse(localStorage.getItem("pk-user"));

    if (!localStorage.getItem("pk-token") && !user) {
        navigate("/");
    }

    let settings = JSON.parse(localStorage.getItem("pk-settings"));

    async function login(token: string) {
        const api = new PKAPI();
        try {
            if (!token) {
                throw new Error("Token cannot be empty.")
            }
            const res: Sys = await api.getSystem({token: token});
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
    
</script>

<!-- display the banner if there's a banner set, and if the current settings allow for it-->
{#if user && user.banner && settings && settings.appearance.banner_top}
<div class="banner" style="background-image: url({user.banner})" />
{/if}
<Container>
    <Row>
        <Col class="mx-auto" xs={12} lg={9}>
            <TabContent class="mt-3">
                <TabPane tabId="system" tab="System" active={tabPane === "system"}>
                    <Card style="border-radius: 0; border: none;">
                        <PrivateSystem bind:user={user}/>
                    </Card>
                </TabPane>
                <TabPane tabId="members" tab="Members" active={tabPane === "members"}>
                    <Card style="border-radius: 0; border: none;">
                        alo
                    </Card>
                </TabPane> 
            </TabContent>
        </Col>
    </Row>
</Container>

<svelte:head>
    <title>pk-webs | dash</title>
</svelte:head>