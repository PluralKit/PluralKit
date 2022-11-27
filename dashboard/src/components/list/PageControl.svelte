<script lang="ts">
    import { Card, CardHeader, CardBody, CardTitle, InputGroupText, InputGroup, Input, Row, Col, Button, Tooltip } from 'sveltestrap';
    import FaList from 'svelte-icons/fa/FaList.svelte'

    import type { ListOptions, PageOptions } from './types';

    export let options: ListOptions;
    export let pageOptions: PageOptions;

    let itemsPerPageSelection = {
        small: 10,
        default: 25,
        large: 50
    }

    $: { if (pageOptions.view === "card") itemsPerPageSelection = {
            small: 12,
            default: 24,
            large: 60
        }
        else {
            itemsPerPageSelection = {
                small: 10,
                default: 25,
                large: 50
            }
        }
    }

    function resetPage() {
    pageOptions.currentPage = 1;
}
</script>

<Card class="mb-3">
    <CardHeader>
        <CardTitle class="d-flex justify-content-between align-items-center mb-0 py-2">
            <div>
                <div class="icon d-inline-block">
                    <FaList />
                </div> {pageOptions.type === 'group' ? 'Member groups' : 'Group members'}
            </div>
        </CardTitle>
    </CardHeader>
    <CardBody>
        <Row class="mb-3">
            <Col xs={12} md={6} lg={4} class="mb-2">
                <InputGroup>
                    <InputGroupText>Page length</InputGroupText>
                    <Input bind:value={pageOptions.itemsPerPage} type="select" aria-label="page length" on:change={() => resetPage()}>
                        <option>{itemsPerPageSelection.small}</option>
                        <option>{itemsPerPageSelection.default}</option>
                        <option>{itemsPerPageSelection.large}</option>
                    </Input>
                </InputGroup>
            </Col>
            <!-- <Col xs={12} md={6} lg={4} class="mb-2">
                <InputGroup>
                    <InputGroupText>Sort by</InputGroupText>
                    <Input bind:value={options.sort} type="select">
                        <option value="name">Name</option>
                        <option value="display_name">Display name</option>
                        <option value="id">ID</option>
                        {#if pageOptions.type === 'member'}
                        <option value="pronouns">Pronouns</option>
                        {/if}
                        <option value="none">API response order</option>
                    </Input>
                </InputGroup>
            </Col> -->
            <Col xs={12} md={6} lg={4} class="mb-2">
                <InputGroup>
                    <InputGroupText>Order</InputGroupText>
                    <Input bind:value={options.order} type="select">
                        <option value="ascending">Ascending</option>
                        <option value="descending">Descending</option>
                    </Input>
                </InputGroup>
            </Col>
            <!-- <Col xs={12} md={6} lg={4} class="mb-2">
                <InputGroup>
                    <InputGroupText>Only show</InputGroupText>
                    <Input bind:value={options.show} type="select" aria-label="view mode" on:change={() => resetPage()}>
                        <option value="all">All {pageOptions.type}s</option>
                        <option value="public">Public {pageOptions.type}s</option>
                        <option value="private">Private {pageOptions.type}s</option>
                    </Input>
                </InputGroup>
            </Col> -->
            <Col xs={12} md={6} lg={4} class="mb-2">
                <InputGroup>
                    <InputGroupText>View mode</InputGroupText>
                    <Input bind:value={pageOptions.view} type="select" aria-label="view mode">
                        <option value="list">List</option>
                        <option value="card">Cards</option>
                    </Input>
                </InputGroup>
            </Col>
        </Row>
        <Row>
            <Col xs={12} class="mb-2">
                <InputGroup class="mb-2">
                    <InputGroupText>Name</InputGroupText>
                    <Input 
                        style="resize: none; overflow: hidden;" 
                        rows={1} type="textarea" 
                        bind:value={options.search.name} 
                        on:keydown={() => resetPage()} 
                        placeholder="Search by name..."/>
                </InputGroup> 
            </Col>
        </Row>
    </CardBody>
</Card>