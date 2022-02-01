<script lang="ts">
    import { Card, CardHeader, CardBody, CardTitle, Alert, Accordion, AccordionItem, InputGroupText, InputGroup, Input, Row, Col, Spinner, Button, Tooltip, Label } from 'sveltestrap';
    import FaUserCircle from 'svelte-icons/fa/FaUserCircle.svelte'
    import { onMount } from 'svelte';
    import FaSearch from 'svelte-icons/fa/FaSearch.svelte'
    import { useParams } from 'svelte-navigator';
    import CardsHeader from '../CardsHeader.svelte';
    import ListPagination from '../ListPagination.svelte';
    import Svelecte, { addFormatter } from 'svelecte';
    import FaLock from 'svelte-icons/fa/FaLock.svelte';
    import Body from './Body.svelte';

    import { Member, Group } from '../../api/types';
    import api from '../../api';

    export let isPublic: boolean;

    export let list: Member[] = [];
    export let groups: Group[] = [];

    $: grouplist = groups && groups.map(function(group) { return {name: group.name, shortid: group.id, id: group.uuid, members: group.members, display_name: group.display_name}; }).sort((a, b) => a.name.localeCompare(b.name));

    let token = localStorage.getItem("pk-token");
    let listLoading = true;
    let err: string;

    let itemsPerPageValue = "25";
    $: itemsPerPage = parseInt(itemsPerPageValue);

    let searchBy = "name";
    let sortBy = "name";
    let sortOrder = "ascending";
    let privacyFilter = "all";
    let groupSearchMode = "include";
    let selectedGroups = [];

    let currentPage = 1;

    let params = useParams();
    $: id = $params.id;

    onMount(() => {
        if (token || isPublic) fetchMembers();
    });

    async function fetchMembers() {
        listLoading = true;
        try {
            const res: Member[] = await api().systems(isPublic ? id : "@me").members.get({ auth: !isPublic });
            list = res;
            listLoading = false;
        } catch (error) {
            console.log(error);
            err = error.message;
            listLoading = false;
        }
    }

    let searchValue: string;

    $: {searchValue; privacyFilter; currentPage = 1};

    $: searchedList = list.filter((item) => {
        if (!searchValue && searchBy !== "description" && searchBy !== "display name") return true;
        
        switch (searchBy) {
            case "name": if (item.name.toLowerCase().includes(searchValue.toLowerCase())) return true;
            break;
            case "display name": if (!searchValue) {
                if (!item.display_name) return true;
                else return false;
            }
            if (item.display_name && item.display_name.toLowerCase().includes(searchValue.toLowerCase())) return true;
            break;
            case "description": if (!searchValue) {
                if (!item.description) return true;
                else return false;
            }
            else if (item.description && item.description.toLowerCase().includes(searchValue.toLowerCase())) return true;
            break;
            case "ID": if (item.id.toLowerCase().includes(searchValue.toLowerCase())) return true;
            break;
            default: if (item.name.toLowerCase().includes(searchValue.toLowerCase())) return true;
            break;
        }

        return false;
    })

    $: filteredList = searchedList.filter((item) => {
        if (privacyFilter === "all") return true;
        if (privacyFilter === "public" && item.privacy.visibility === "public") return true;
        if (privacyFilter === "private" && item.privacy.visibility === "private") return true;
        return false;
    });
    
    let sortedList = [];

    $: if (filteredList) {
        switch (sortBy) {
            case "name": sortedList = filteredList.sort((a, b) => a.name.localeCompare(b.name));
            break;
            case "display name": sortedList = filteredList.sort((a, b) => {
                if (a.display_name && b.display_name) return a.display_name.localeCompare(b.display_name);
                else if (a.display_name && !b.display_name) return a.display_name.localeCompare(b.name);
                else if (!a.display_name && b.display_name) return a.name.localeCompare(b.display_name);
                else return a.name.localeCompare(b.name);
            });
            break;
            case "creation date": sortedList = filteredList.sort((a, b) => {
                if (a.created && b.created) return a.created.localeCompare(b.created);
            });
            break;
            case "ID": sortedList = filteredList.sort((a, b) => a.id.localeCompare(b.id));
            break;
            default: sortedList = filteredList.sort((a, b) => a.name.localeCompare(b.name));
            break;
        }
    }

    let memberFilteredList = [];
    $: memberFilteredList = sortedList.filter((item: Member) => {      
        if (groupSearchMode === "none") {
            if (groups.some(group => group.members && group.members.includes(item.uuid))) return false;
        }

        if (selectedGroups.length < 1) return true;

        switch (groupSearchMode) {
            case "include": if (selectedGroups.some(group => group.members && group.members.includes(item.uuid))) return true;
            break;
            case "exclude": if (selectedGroups.every(group => group.members && !group.members.includes(item.uuid))) return true;
            break;
            case "match": if (selectedGroups.every(group => group.members && group.members.includes(item.uuid))) return true;
            break;
            default: return true;
        }

        return false;
    })

    let finalList = [];
    $:{sortOrder; if (sortOrder === "descending") finalList = memberFilteredList.reverse(); else finalList = memberFilteredList;}

    $: finalList = finalList;

    $: indexOfLastItem = currentPage * itemsPerPage;
    $: indexOfFirstItem = indexOfLastItem - itemsPerPage;
    $: pageAmount = Math.ceil(finalList.length / itemsPerPage);

    $: slicedList = finalList.slice(indexOfFirstItem, indexOfLastItem);

    function groupListRenderer(item: any) {
    return `${item.name} (<code>${item.shortid}</code>)`;
  }

  addFormatter({
    'member-list': groupListRenderer
  });

  function updateList(event: any) {
      list = list.map(member => member.id !== event.detail.id ? member : event.detail);
  }
  
  function updateGroups(event: any) {
      groups = event.detail;
  }

  function updateDelete(event: any) {
      list = list.filter(member => member.id !== event.detail);
  }
</script>

<Card class="mb-3">
    <CardHeader>
        <CardTitle>
            <CardTitle style="margin-top: 8px; outline: none;">
                <div class="icon d-inline-block">
                    <FaSearch />
                </div> Search members
            </CardTitle>
        </CardTitle>
    </CardHeader>
    <CardBody>
        <Row>
            <Col xs={12} lg={3} class="mb-2">
                <InputGroup>
                    <InputGroupText>Page length</InputGroupText>
                    <Input bind:value={itemsPerPageValue} type="select">
                        <option>10</option>
                        <option>25</option>
                        <option>50</option>
                    </Input>
                </InputGroup>
            </Col>
            <Col xs={12} lg={3} class="mb-2">
                <InputGroup>
                    <InputGroupText>Search by</InputGroupText>
                    <Input bind:value={searchBy} type="select">
                        <option>name</option>
                        <option>display name</option>
                        <option>description</option>
                        <option>ID</option>
                    </Input>
                </InputGroup>
            </Col>
            <Col xs={12} lg={3} class="mb-2">
                <InputGroup>
                    <InputGroupText>Sort by</InputGroupText>
                    <Input bind:value={sortBy} type="select">
                        <option>name</option>
                        <option>display name</option>
                        {#if !isPublic}<option>creation date</option>{/if}
                        <option>ID</option>
                    </Input>
                </InputGroup>
            </Col>
            <Col xs={12} lg={3} class="mb-2">
                <InputGroup>
                    <InputGroupText>Sort order</InputGroupText>
                    <Input bind:value={sortOrder} type="select">
                        <option>ascending</option>
                        <option>descending</option>
                    </Input>
                </InputGroup>
            </Col>
            {#if !isPublic}
            <Col xs={12} lg={3} class="mb-2">
                <InputGroup>
                    <InputGroupText>Only show</InputGroupText>
                    <Input bind:value={privacyFilter} type="select">
                        <option>all</option>
                        <option>public</option>
                        <option>private</option>
                    </Input>
                </InputGroup>
            </Col>
            {/if}
        </Row>
        {#if !isPublic}
        <hr/>
        <Label>Filter members by group</Label>
        <Svelecte disableHighlight renderer="member-list" valueAsObject bind:value={selectedGroups} options={grouplist} multiple style="margin-bottom: 0.5rem">
        </Svelecte>
        <span style="cursor: pointer" id="m-include" on:click={() => groupSearchMode = "include"}>{@html groupSearchMode === "include" ? "<b>include</b>" : "include"}</span>
         | <span style="cursor: pointer" id="m-exclude" on:click={() => groupSearchMode = "exclude"}>{@html groupSearchMode === "exclude" ? "<b>exclude</b>" : "exclude"}</span> 
         | <span style="cursor: pointer" id="m-match" on:click={() => groupSearchMode = "match"}>{@html groupSearchMode === "match" ? "<b>exact match</b>" : "exact match"}</span>
         | <span style="cursor: pointer" id="m-none" on:click={() => groupSearchMode = "none"}>{@html groupSearchMode === "none" ? "<b>none</b>" : "none"}</span>
        <Tooltip placement="bottom" target="m-include">Includes every member who's a part of any of the groups.</Tooltip>
        <Tooltip placement="bottom" target="m-exclude">Excludes every member who's a part of any of the groups, the opposite of include.</Tooltip>
        <Tooltip placement="bottom" target="m-match">Only includes members who are a part of every group.</Tooltip>
        <Tooltip placement="bottom" target="m-none">Only includes members that are in no groups.</Tooltip>
        {/if}
    </CardBody>
</Card>


{#if listLoading && !err}
    <div class="mx-auto text-center">
        <Spinner class="d-inline-block" />
    </div>
{:else if err}
<Alert color="danger">{err}</Alert>
{:else}

<Row>
    <Col xs={12} lg={10}>
        <Input class="mb-3" bind:value={searchValue} placeholder="search by {searchBy}..."/>
    </Col>
    <Col xs={12} lg={2} class="mb-3 mb-lg-0">
        <Button class="w-100 mb-3" color="primary" on:click={fetchMembers}>Refresh</Button>
    </Col>
</Row>
<ListPagination bind:currentPage bind:pageAmount />
<Accordion class="my-3" stayOpen>
    {#each slicedList as member, index (member.id)}
            {#if (!isPublic && member.privacy.visibility === "public") || isPublic}
            <AccordionItem>
                <CardsHeader bind:item={member} slot="header">
                    <FaUserCircle slot="icon" />
                </CardsHeader>
                <Body on:deletion={updateDelete} on:update={updateList} on:updateGroups={updateGroups} bind:isPublic bind:groups bind:member />
            </AccordionItem>
            {:else}
            <AccordionItem>
                <CardsHeader bind:item={member} slot="header">
                    <FaLock slot="icon" />
                </CardsHeader>
                <Body on:deletion={updateDelete} on:update={updateList} on:updateGroups={updateGroups} bind:isPublic bind:groups bind:member />
            </AccordionItem>
            {/if}
    {/each}
</Accordion>
<ListPagination bind:currentPage bind:pageAmount />
{/if}