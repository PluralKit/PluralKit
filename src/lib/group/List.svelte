<script lang="ts">
    import { Card, CardHeader, CardBody, CardTitle, Alert, Accordion, AccordionItem, InputGroupText, InputGroup, Input, Row, Col, Spinner, Button } from 'sveltestrap';
    import FaUserCircle from 'svelte-icons/fa/FaUserCircle.svelte'
    import { onMount } from 'svelte';
    import FaSearch from 'svelte-icons/fa/FaSearch.svelte'
    import { useParams } from 'svelte-navigator';
    import type Group from '../../api/group'
    import PKAPI from '../../api';
    import CardsHeader from '../CardsHeader.svelte';
    import ListPagination from '../ListPagination.svelte';

    export let isPublic: boolean;
    let itemLoading = false;

    export let list: Group[] = [];
    let token = localStorage.getItem("pk-token");
    let listLoading = true;
    let err: string;

    let itemsPerPageValue = "25";
    $: itemsPerPage = parseInt(itemsPerPageValue);

    let searchBy = "name";
    let sortBy = "name";
    let sortOrder = "ascending";
    let privacyFilter = "all";

    let currentPage = 1;

    let params = useParams();
    $: id = $params.id;

    onMount(() => {
        if (token || isPublic) fetchGroups();
    });

    const api = new PKAPI();

    async function fetchGroups() {
        listLoading = true;
        try {
            const res: Group[] = await api.getGroupList({token: !isPublic && token, id: isPublic && id});
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
        if (!searchValue) return true;
        
        switch (searchBy) {
            case "name": if (item.name.toLowerCase().includes(searchValue.toLowerCase())) return true;
            break;
            case "display name": if (item.display_name && item.display_name.toLowerCase().includes(searchValue.toLowerCase())) return true;
            break;
            case "description": if (item.description && item.description.toLowerCase().includes(searchValue.toLowerCase())) return true;
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
            case "creation date": sortedList = filteredList.sort((a, b) => a.created.localeCompare(b.created));
            break;
            case "ID": sortedList = filteredList.sort((a, b) => a.id.localeCompare(b.id));
            break;
            default: sortedList = filteredList.sort((a, b) => a.name.localeCompare(b.name));
            break;
        }
    }

    $: if (sortOrder === "descending") sortedList = sortedList.reverse();

    $: indexOfLastItem = currentPage * itemsPerPage;
    $: indexOfFirstItem = indexOfLastItem - itemsPerPage;
    $: pageAmount = Math.ceil(sortedList.length / itemsPerPage);

    $: slicedList = sortedList.slice(indexOfFirstItem, indexOfLastItem);

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
            <Col xs={12} lg={4} class="mb-2">
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
    <Col xs={12} lg={10} class="mb-2 mb-lg-0">
        <Input class="mb-3" bind:value={searchValue} placeholder="search by {searchBy}..."/>
    </Col>
    <Col xs={12} lg={2}>
        <Button class="w-100" color="primary" on:click={fetchGroups}>Refresh</Button>
    </Col>
</Row>
<ListPagination bind:currentPage bind:pageAmount />
<Accordion class="my-3" stayOpen>
    {#each slicedList as group (group.id)}
        <AccordionItem>
            <CardsHeader bind:item={group} bind:loading={itemLoading} slot="header">
                <FaUserCircle slot="icon"/>
            </CardsHeader>
        </AccordionItem>
    {/each}
</Accordion>
<ListPagination bind:currentPage bind:pageAmount />
{/if}