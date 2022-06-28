<script lang="ts">
    import { Container, Row, Col, Alert, Spinner, Card, CardHeader, CardBody, CardTitle, Tooltip } from "sveltestrap";
    import Body from '../lib/member/Body.svelte';
    import CardsList from '../lib/list/CardsList.svelte';
    import { useParams, Link, navigate } from 'svelte-navigator';
    import { onMount } from 'svelte';
    import api from "../api";
    import { Member, Group } from "../api/types";
    import CardsHeader from "../lib/CardsHeader.svelte";
    import FaAddressCard from 'svelte-icons/fa/FaAddressCard.svelte'
    import FaList from 'svelte-icons/fa/FaList.svelte'
    import ListPagination from '../lib/ListPagination.svelte';

    let loading = true;
    let groupLoading = false;
    let params = useParams();
    let err = "";
    let groupErr = "";
    let member: Member;
    let groups: Group[] = [];
    let systemGroups: Group[] = [];
    let systemMembers: Member[] = [];
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
    $: pageAmount = Math.ceil(groups.length / itemsPerPage);

    $: orderedGroups = groups.sort((a, b) => a.name.localeCompare(b.name));
    $: slicedGroups = orderedGroups.slice(indexOfFirstItem, indexOfLastItem);

    if (!isPublic && isPage) {
        let user = localStorage.getItem("pk-user");
        if (!user) navigate("/");
    }

    onMount(() => {
        fetchMember();
    });

    let title = isPublic ? "member" : "member (dash)";

    async function fetchMember() {
        try {
            member = await api().members($params.id).get({auth: !isPublic});
            if (!isPublic && !member.privacy) {
                notOwnSystem = true;
                throw new Error("Member is not from own system.");
            }
            err = "";
            loading = false;
            if (member.name) {
                title = isPublic ? member.name : `${member.name} (dash)`;
            }
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
            groups = await api().members($params.id).groups().get({auth: !isPublic, query: { with_members: !isPublic } });
            if (!isPublic) {
                await new Promise(resolve => setTimeout(resolve, 1000));
                systemGroups = await api().systems("@me").groups.get({ auth: true, query: { with_members: true } });
            }
            groupErr = "";
            groupLoading = false;
            // we can't use with_members from a group list from a member endpoint yet, but I'm leaving this in in case we do
            // (this is needed for editing a group member list from the member page)
            /* if (!isPublic) {
                await new Promise(resolve => setTimeout(resolve, 1000));
                systemMembers = await api().systems("@me").members.get({auth: true});
            } */
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

    function updateDelete() {
        isDeleted = true;
    }
    
    function deleteGroupFromList(event: any) {
        groups = groups.filter(group => group.id !== event.detail);
        systemGroups = systemGroups.filter(group => group.id !== event.detail);
  }

    async function copyShortLink(event?) {
        if (event) {
            let ctrlDown = event.ctrlKey||event.metaKey; // mac support
            if (!(ctrlDown && event.key === "c") && event.key !== "Enter") return;
        }
        try {
            await navigator.clipboard.writeText(`https://pk.mt/m/${member.id}`);
            copied = true;
            await new Promise(resolve => setTimeout(resolve, 2000));
            copied = false;
        } catch (error) {
            console.log(error);
        }
    }
</script>

{#if settings && settings.appearance.color_background && !notOwnSystem}
    <div class="background" style="background-color: {member && `#${member.color}`}"></div>
{/if}
{#if member && member.banner && settings && settings.appearance.banner_top && !notOwnSystem}
<div class="banner" style="background-image: url({member.banner})" />
{/if}
<Container>
    <Row>
        <Col class="mx-auto" xs={12} lg={11} xl={10}>
            <h2 class="visually-hidden">Viewing {isPublic ? "a public" : "your own"} member</h2>
            {#if isDeleted}
                <Alert color="success">Member has been successfully deleted. <Link to="/dash">Return to dash</Link></Alert>
            {:else}
            {#if isPublic}
                <Alert color="info" aria-hidden>You are currently <b>viewing</b> a member.</Alert>
            {/if}
            {#if notOwnSystem}
                <Alert color="danger">This member does not belong to your system, did you mean to look up <Link to={`/profile/m/${member.id}`}>their public page</Link>?</Alert>
            {:else if err}
                <Alert color="danger">{@html err}</Alert>
            {:else if loading}
                <Spinner/>
            {:else if member && member.id}
                <Card class="mb-4">
                    <CardHeader>
                        <CardsHeader bind:item={member}>
                            <div slot="icon" style="cursor: pointer;" id={`member-copy-${member.id}`} on:click|stopPropagation={() => copyShortLink()} on:keydown={(e) => copyShortLink(e)} tabindex={0} >
                                <FaAddressCard slot="icon" />
                            </div>
                        </CardsHeader>
                        <Tooltip placement="top" target={`member-copy-${member.id}`}>{copied ? "Copied!" : "Copy public link"}</Tooltip>
                    </CardHeader>
                    <CardBody>
                        <Body on:deletion={updateDelete} on:updateGroups={updateGroups} bind:groups={systemGroups} bind:member={member} isPage={isPage} isPublic={isPublic}/>
                    </CardBody>
                </Card>
            {/if}
            {#if groupLoading}
                <Alert color="primary"><Spinner size="sm" /> Fetching groups...</Alert>
            {:else if groupErr}
                <Alert color="danger">{groupErr}</Alert>
            {:else if groups && groups.length > 0}
            <Card class="mb-2">
                <CardHeader>
                    <CardTitle style="margin-top: 8px; outline: none;">
                        <div class="icon d-inline-block">
                            <FaList />
                        </div> Member groups
                    </CardTitle>
                </CardHeader>
            </Card>
            <ListPagination bind:currentPage bind:pageAmount />
            <CardsList on:deletion={(e) => deleteGroupFromList(e)} bind:list={slicedGroups} isPublic={isPublic} itemType="group" itemsPerPage={itemsPerPage} currentPage={currentPage} fullLength={groups.length} />
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