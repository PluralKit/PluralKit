<script lang="ts">
    import { Container, Row, Col, Alert, Spinner, Card, CardHeader, CardBody } from "sveltestrap";
    import Body from '../lib/member/Body.svelte';
    import { useParams } from 'svelte-navigator';
    import { onMount } from 'svelte';
    import api from "../api";
    import { Member, Group } from "../api/types";
    import CardsHeader from "../lib/CardsHeader.svelte";
    import FaAddressCard from 'svelte-icons/fa/FaAddressCard.svelte'

    let loading = true;
    let groupLoading = false;
    let params = useParams();
    let err = "";
    let groupErr = "";
    let member: Member;
    let groups: Group[];
    let systemGroups: Group[];
    const isPage = true;
    export let isPublic = true;
    let settings = JSON.parse(localStorage.getItem("pk-settings"));

    onMount(() => {
        fetchMember();
    });

    async function fetchMember() {
        try {
            member = await api().members($params.id).get({auth: !isPublic});
            if (!isPublic && !member.privacy) throw new Error("This member does not belong to your system, did you mean to look up their public page?")
            err = "";
            loading = false;
            groupLoading = true;
            await new Promise(resolve => setTimeout(resolve, 1000));
            fetchGroups();
        } catch (error) {
            console.log(error);
            err = error.message;
            loading = false;
        }
    }

    async function fetchGroups() {
        try {
            groups = await api().members($params.id).groups().get({auth: !isPublic});
            if (!isPublic) {
                await new Promise(resolve => setTimeout(resolve, 1000));
                systemGroups = await api().systems("@me").groups.get({ auth: true, query: { with_members: true } });
            }
            groupErr = "";
            groupLoading = false;
        } catch (error) {
            console.log(error);
            groupErr = error.message;
            groupLoading = false;
        }
    }

    async function updateGroups() {
      groupLoading = true;
      await new Promise(resolve => setTimeout(resolve, 500));
      fetchGroups();
    }
</script>

{#if settings && settings.appearance.color_background}
    <div class="background" style="background-color: {member && `#${member.color}`}"></div>
{/if}
{#if member && member.banner && ((settings && settings.appearance.banner_top) || !settings)}
<div class="banner" style="background-image: url({member.banner})" />
{/if}
<Container>
    <Row>
        <Col class="mx-auto" xs={12} lg={11} xl={10}>
            {#if err}
                <Alert color="danger">{err}</Alert>
            {:else if loading}
                <Spinner/>
            {:else if member && member.id}
                <Card class="mb-3">
                    <CardHeader>
                        <CardsHeader bind:item={member}>
                            <FaAddressCard slot="icon" />
                        </CardsHeader>
                    </CardHeader>
                    <CardBody>
                        <Body on:updateGroups={updateGroups} bind:groups={systemGroups} bind:member={member} isPage={isPage} isPublic={isPublic}/>
                    </CardBody>
                </Card>
            {/if}
            {#if groupLoading}
                fetching groups...
            {:else if groupErr}
                <Alert color="danger">{groupErr}</Alert>
            {:else if groups && groups.length > 0}
                {#each groups as group}
                    {group.name} ({group.id})<br/>
                {/each}
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
</style>