<script lang="ts">
    import { Container, Col, Row, TabContent, TabPane, Alert, Spinner } from 'sveltestrap';
    import { useParams } from "svelte-navigator";
    import { onMount } from 'svelte';
    
    import System from '../../lib/system/Main.svelte';
    import PKAPI from '../../api';
    import Sys from '../../api/system';
    import MemberList from '../../lib/member/List.svelte';
    import GroupList from '../../lib/group/List.svelte';

    let isPublic = true;

    let user = new Sys({});
    let settings = JSON.parse(localStorage.getItem("pk-settings"));

    let members = [];
    let groups = [];

    let params = useParams();
    $: id = $params.id;
    
    let err: string;
    
    const api = new PKAPI();

    let title = "system"

    onMount(() => {
        getSystem();
    })

    async function getSystem() {
        try {
            let res: Sys = await api.getSystem({id: id})
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
        <Col class="mx-auto" xs={12} lg={10}>
            {#if !user.id && !err}
            <div class="mx-auto text-center">
                <Spinner class="d-inline-block" />
            </div>
            {:else if err}
                <Alert color="danger">{err}</Alert>
            {:else}
            <Alert color="info">You are currently <b>viewing</b> a system.</Alert>
            <TabContent class="mt-3">
                <TabPane tabId="system" tab="System" active>
                        <System bind:user bind:isPublic />
                </TabPane>
                <TabPane tabId="members" tab="Members">
                        <MemberList bind:list={members} bind:isPublic/>
                </TabPane>
                <TabPane tabId="groups" tab="Groups">
                    <GroupList bind:members={members} bind:list={groups} bind:isPublic/>
            </TabPane> 
            </TabContent>
            {/if}
        </Col>
    </Row>
</Container>

<svelte:head>
    <title>pk-webs | {title}</title>
</svelte:head>