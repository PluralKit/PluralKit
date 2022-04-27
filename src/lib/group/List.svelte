<script lang="ts">
    import { Link } from 'svelte-navigator';
    import { Card, CardHeader, CardBody, CardTitle, Alert, Accordion, AccordionItem, InputGroupText, InputGroup, Input, Label, Row, Col, Spinner, Button, Tooltip } from 'sveltestrap';
    import FaUsers from 'svelte-icons/fa/FaUsers.svelte'
    import { onMount } from 'svelte';
    import FaSearch from 'svelte-icons/fa/FaSearch.svelte'
    import { useParams } from 'svelte-navigator';
    import CardsHeader from '../CardsHeader.svelte';
    import ListPagination from '../ListPagination.svelte';
    import Body from './Body.svelte';
    import Svelecte, { addFormatter } from 'svelecte';
    import FaLock from 'svelte-icons/fa/FaLock.svelte';
    import NewGroup from './NewGroup.svelte';

    import { Member, Group } from '../../api/types';
    import api from '../../api';

    export let isPublic: boolean;

    export let list: Group[];
    export let members: Member[];

    $: memberlist = members && members.map(function(member) { return {name: member.name, shortid: member.id, id: member.uuid, display_name: member.display_name}; }).sort((a, b) => a.name.localeCompare(b.name));

    let token = localStorage.getItem("pk-token");
    let listLoading = true;
    let err: string;

    let settings = JSON.parse(localStorage.getItem("pk-settings"));

    let itemsPerPageValue = settings && settings.accessibility && settings.accessibility.expandedcards ? "10" : "25";
    $: itemsPerPage = parseInt(itemsPerPageValue);

    let searchBy = "name";
    let sortBy = "name";
    let sortOrder = "ascending";
    let privacyFilter = "all";
    let memberSearchMode = "include";
    let selectedMembers = [];

    let currentPage = 1;

    let params = useParams();
    $: id = $params.id;

    onMount(() => {
        if (token || isPublic) fetchGroups();
    });


    async function fetchGroups() {
        err = "";
        listLoading = true;
        try {
            const res: Group[] = await api().systems(isPublic ? id : "@me").groups.get({ auth: !isPublic, query: { with_members: !isPublic } });
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
    $: memberFilteredList = sortedList.filter((item: Group) => {
        if (memberSearchMode === "none") {
            if (item.members && item.members.length > 0) return false;
        }

        if (selectedMembers.length < 1) return true;

        switch (memberSearchMode) {
            case "include": if (item.members && selectedMembers.some(value => item.members.includes(value))) return true;
            break;
            case "exclude": if (item.members && selectedMembers.every(value => !item.members.includes(value))) return true;
            break;
            case "match": if (item.members && selectedMembers.every(value => item.members.includes(value))) return true;
            break;
            default: return true;
        }

        return false;
    });

    let finalList = [];
    $:{sortOrder; if (sortOrder === "descending") finalList = memberFilteredList.reverse(); else finalList = memberFilteredList;}

    $: finalList = finalList;

    $: indexOfLastItem = currentPage * itemsPerPage;
    $: indexOfFirstItem = indexOfLastItem - itemsPerPage;
    $: pageAmount = Math.ceil(finalList.length / itemsPerPage);

    let slicedList = [];
    $: slicedList = finalList.slice(indexOfFirstItem, indexOfLastItem);

    function memberListRenderer(item: any) {
    return `${item.name} (<code>${item.shortid}</code>)`;
  }

  addFormatter({
    'member-list': memberListRenderer
  });

  function updateList(event: any) {
      list = list.map(group => group.id !== event.detail.id ? group : event.detail)
  }

  function updateDelete(event: any) {
      list = list.filter(group => group.id !== event.detail);
  }

  function addGroupToList(event: any) {
      let group = event.detail;
      group.members = members;
      list.push(group);
      list = list;
  }
</script>

<Card class="mb-3">
    <CardHeader>
        <CardTitle>
            <CardTitle style="margin-top: 8px; outline: none;">
                <div class="icon d-inline-block">
                    <FaSearch />
                </div> Search groups
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
        <Label>Filter groups by member</Label>
        <Svelecte renderer="member-list" bind:value={selectedMembers} disableHighlight options={memberlist} multiple style="margin-bottom: 0.5rem">
        </Svelecte>
        <span style="cursor: pointer" id="g-include" on:click={() => memberSearchMode = "include"}>{@html memberSearchMode === "include" ? "<b>include</b>" : "include"}</span>
         | <span style="cursor: pointer" id="g-exclude" on:click={() => memberSearchMode = "exclude"}>{@html memberSearchMode === "exclude" ? "<b>exclude</b>" : "exclude"}</span> 
         | <span style="cursor: pointer" id="g-match" on:click={() => memberSearchMode = "match"}>{@html memberSearchMode === "match" ? "<b>exact match</b>" : "exact match"}</span>
         | <span style="cursor: pointer" id="g-none" on:click={() => memberSearchMode = "none"}>{@html memberSearchMode === "none" ? "<b>none</b>" : "none"}</span>
        <Tooltip placement="bottom" target="g-include">Includes every group with any of the members.</Tooltip>
        <Tooltip placement="bottom" target="g-exclude">Excludes every group with any of the members, opposite of include.</Tooltip>
        <Tooltip placement="bottom" target="g-match">Only includes groups which have all the members selected.</Tooltip>
        <Tooltip placement="bottom" target="g-none">Only includes groups that have no members.</Tooltip>
        {/if}
    </CardBody>
</Card>


{#if listLoading && !err}
    <div class="mx-auto text-center">
        <Spinner class="d-inline-block" />
    </div>
{:else if err}
<Row>
    <Col xs={12} lg={10}>
        <Alert color="danger">{err}</Alert>
    </Col>
    <Col xs={12} lg={2}>
        <Button class="w-100 mb-3" color="primary" on:click={fetchGroups}>Refresh</Button>
    </Col>
</Row>
{:else}

<Row>
    <Col xs={12} lg={10} class="mb-2 mb-lg-0">
        <Input class="mb-3" bind:value={searchValue} placeholder="search by {searchBy}..."/>
    </Col>
    <Col xs={12} lg={2}>
        <Button class="w-100 mb-3" color="primary" on:click={fetchGroups}>Refresh</Button>
    </Col>
</Row>
<ListPagination bind:currentPage bind:pageAmount />
{#if !isPublic}
<NewGroup on:create={addGroupToList} />
{/if}
{#if settings && settings.accessibility ? (!settings.accessibility.expandedcards && !settings.accessibility.pagelinks) : true}
<Accordion class="my-3" stayOpen>
    {#each slicedList as group, index (group.id)}
        {#if (!isPublic && group.privacy.visibility === "public") || isPublic}
        <AccordionItem>
            <CardsHeader bind:item={group} slot="header">
                <FaUsers slot="icon" />
            </CardsHeader>
            <Body on:deletion={updateDelete} on:update={updateList} on:updateMembers={updateList} bind:members bind:group bind:isPublic={isPublic}/>
        </AccordionItem>
        {:else}
        <AccordionItem>
            <CardsHeader bind:item={group} slot="header">
                <FaLock slot="icon" />
            </CardsHeader>
            <Body on:deletion={updateDelete} on:update={updateList} on:updateMembers={updateList} bind:members bind:group bind:isPublic={isPublic}/>
        </AccordionItem>
        {/if}
    {/each}
</Accordion>
{:else if settings.accessibility.expandedcards}
    {#each slicedList as group, index (group.id)}
    {#if (!isPublic && group.privacy.visibility === "public") || isPublic}
    <Card class="mb-3">
        <CardHeader>
            <CardsHeader item={group}>
                <FaUsers slot="icon" />
            </CardsHeader>
        </CardHeader>
        <CardBody>
            <Body on:deletion={updateDelete} on:update={updateList} on:updateMembers={updateList} isPublic={isPublic} bind:members bind:group />
        </CardBody>
    </Card>
    {:else}
    <Card class="mb-3">
        <CardHeader>
            <CardsHeader item={group}>
                <FaLock slot="icon" />
            </CardsHeader>
        </CardHeader>
        <CardBody>
            <Body on:deletion={updateDelete} on:update={updateList} on:updateMembers={updateList} isPublic={isPublic} bind:group bind:members />
        </CardBody>
    </Card>
    {/if}
    {/each}
{:else}
    <div class="my-3">
    {#each slicedList as group, index (group.id)}
    {#if (!isPublic && group.privacy.visibility === "public") || isPublic}
    <Card>
        <Link class="accordion-button collapsed" style="text-decoration: none;" to={!isPublic ? `/dash/g/${group.id}` : `/profile/g/${group.id}`}>
            <CardsHeader bind:item={group}>
                <FaUsers slot="icon" />
            </CardsHeader>
        </Link>
    </Card>
    {:else}
    <Card>
        <Link class="accordion-button collapsed" style="text-decoration: none;" to={!isPublic ? `/dash/g/${group.id}` : `/profile/g/${group.id}`}>
            <CardsHeader bind:item={group}>
                <FaLock slot="icon" />
            </CardsHeader>
        </Link>
    </Card>
    {/if}
    {/each}
    </div>
{/if}
<ListPagination bind:currentPage bind:pageAmount />
{/if}