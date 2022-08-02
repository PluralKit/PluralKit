<script lang="ts">
    import { Container, Row, Col, Alert, Spinner, Card, CardHeader, CardBody, CardTitle, Tooltip } from "sveltestrap";
    import Body from '../lib/group/Body.svelte';
    import { useParams, Link, navigate } from 'svelte-navigator';
    import { onMount } from 'svelte';
    import api from "../api";
    import { Member, Group } from "../api/types";
    import CardsHeader from "../lib/CardsHeader.svelte";
    import FaUsers from 'svelte-icons/fa/FaUsers.svelte';
    import FaList from 'svelte-icons/fa/FaList.svelte';
    import ListPagination from '../lib/ListPagination.svelte';
    import CardsList from '../lib/list/CardsList.svelte';

    let loading = true;
    let memberLoading = false;
    let params = useParams();
    let err = "";
    let memberErr = "";
    let group: Group;
    let members: Member[] = [];
    let systemMembers: Group[] = [];
    let isDeleted = false;
    let notOwnSystem = false;
    let copied = false;

    const isPage = true;
    export let isPublic = true;
    let settings = JSON.parse(localStorage.getItem("pk-settings"));

    let currentPage = 1;
    let itemsPerPage = settings && settings.accessibility && settings.accessibility.expandedcards ? 5 : 10;

    $: indexOfLastItem = currentPage * itemsPerPage;
    $: indexOfFirstItem = indexOfLastItem - itemsPerPage;
    $: pageAmount = Math.ceil(members.length / itemsPerPage);

    $: orderedMembers = members.sort((a, b) => a.name.localeCompare(b.name));
    $: slicedMembers = orderedMembers.slice(indexOfFirstItem, indexOfLastItem);

    if (!isPublic && isPage) {
        let user = localStorage.getItem("pk-user");
        if (!user) navigate("/");
    }

    onMount(() => {
        fetchGroup();
    });

    let title = isPublic ? "group" : "group (dash)";

    async function fetchGroup() {
        try {
            group = await api().groups($params.id).get({auth: !isPublic});
            if (!isPublic && !group.privacy) {
                notOwnSystem = true;
                throw new Error("Group is not from own system.");
            }
            err = "";
            loading = false;
            if (group.name) {
                title = isPublic ? group.name : `${group.name} (dash)`;
            }
            memberLoading = true;
            await new Promise(resolve => setTimeout(resolve, 1000));
            fetchMembers();
        } catch (error) {
            console.log(error);
            err = error.message;
            loading = false;
        }
    }

    async function fetchMembers() {
        try {
            members = await api().groups($params.id).members().get({auth: !isPublic});
            group.members = members.map(function(member) {return member.uuid});
            if (!isPublic) {
                await new Promise(resolve => setTimeout(resolve, 1000));
                systemMembers = await api().systems("@me").members.get({ auth: true });
            }
            memberErr = "";
            memberLoading = false;
        } catch (error) {
            console.log(error);
            memberErr = error.message;
            memberLoading = false;
        }
    }

    async function updateMembers() {
      memberLoading = true;
      await new Promise(resolve => setTimeout(resolve, 500));
      fetchMembers();
    }

    function updateDelete() {
        isDeleted = true;
    }
    
    function updateMemberList(event: any) {
        members = members.map(member => member.id !== event.detail.id ? member : event.detail);
        systemMembers = systemMembers.map(member => member.id !== event.detail.id ? member : event.detail);
    }

    function deleteMemberFromList(event: any) {
        members = members.filter(member => member.id !== event.detail);
        systemMembers = systemMembers.filter(member => member.id !== event.detail);
  }

    async function copyShortLink(event?) {
        if (event) {
            let ctrlDown = event.ctrlKey||event.metaKey; // mac support
            if (!(ctrlDown && event.key === "c") && event.key !== "Enter") return;
        }
        try {
            await navigator.clipboard.writeText(`https://pk.mt/g/${group.id}`);
            copied = true;
            await new Promise(resolve => setTimeout(resolve, 2000));
            copied = false;
        } catch (error) {
            console.log(error);
        }
    }
</script>

{#if settings && settings.appearance.color_background && !notOwnSystem}
    <div class="background" style="background-color: {group && `#${group.color}`}"></div>
{/if}
{#if group && group.banner && settings && settings.appearance.banner_top && !notOwnSystem}
<div class="banner" style="background-image: url({group.banner})" />
{/if}
<Container>
    <Row>
        <Col class="mx-auto" xs={12} lg={11} xl={10}>
            <h2 class="visually-hidden">Viewing {isPublic ? "a public" : "your own"} group</h2>
            {#if isDeleted}
                <Alert color="success">Group has been successfully deleted. <Link to="/dash">Return to dash</Link></Alert>
            {:else}
            {#if isPublic}
                <Alert color="info" aria-hidden>You are currently <b>viewing</b> a group.</Alert>
            {/if}
            {#if notOwnSystem}
                <Alert color="danger">This group does not belong to your system, did you mean to look up <Link to={`/profile/g/${group.id}`}>it's public page</Link>?</Alert>
            {:else if err}
                <Alert color="danger">{@html err}</Alert>
            {:else if loading}
                <Spinner/>
            {:else if group && group.id}
                <Card class="mb-4">
                    <CardHeader>
                        <CardsHeader bind:item={group}>
                            <div slot="icon" style="cursor: pointer;" id={`group-copy-${group.id}`} on:click|stopPropagation={() => copyShortLink()} on:keydown={(e) => copyShortLink(e)} tabindex={0} >
                                <FaUsers slot="icon" />
                            </div>
                        </CardsHeader>
                        <Tooltip placement="top" target={`group-copy-${group.id}`}>{copied ? "Copied!" : "Copy public link"}</Tooltip>
                    </CardHeader>
                    <CardBody>
                        <Body on:deletion={updateDelete} on:updateMembers={updateMembers} bind:members={systemMembers} bind:group={group} isPage={isPage} isPublic={isPublic}/>
                    </CardBody>
                </Card>
            {/if}
            {#if memberLoading}
                <Alert color="primary"><Spinner size="sm" /> Fetching members...</Alert>
            {:else if memberErr}
                <Alert color="danger">{memberErr}</Alert>
            {:else if members && members.length > 0}
            <Card class="mb-2">
                <CardHeader>
                    <CardTitle style="margin-top: 8px; outline: none;">
                        <div class="icon d-inline-block">
                            <FaList />
                        </div> Group list
                    </CardTitle>
                </CardHeader>
            </Card>
            <ListPagination bind:currentPage bind:pageAmount />
            <CardsList on:deletion={(e) => deleteMemberFromList(e)} bind:list={slicedMembers} isPublic={isPublic} itemType="member" itemsPerPage={itemsPerPage} currentPage={currentPage} fullLength={members.length} />
            <ListPagination bind:currentPage bind:pageAmount />
            {/if}
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

<svelte:head>
    <title>PluralKit | {title}</title>
</svelte:head>

