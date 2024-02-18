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
        else if (pageOptions.view === "tiny") itemsPerPageSelection = {
            small: 18,
            default: 36,
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

    // $: updateItemsPerPage(pageOptions.view)

    // function updateItemsPerPage(..._: any) {
    //     if (pageOptions.view === "card") pageOptions.itemsPerPage = 24
    //     else if (pageOptions.view === "tiny") pageOptions.itemsPerPage = 36
    //     else pageOptions.itemsPerPage = 25
    // }
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
                    <Input bind:value={pageOptions.itemsPerPage} type="select" aria-label="page length">
                        <option value={itemsPerPageSelection.small}>{itemsPerPageSelection.small}</option>
                        <option value={itemsPerPageSelection.default}>{itemsPerPageSelection.default}</option>
                        <option value={itemsPerPageSelection.large}>{itemsPerPageSelection.large}</option>
                    </Input>
                </InputGroup>
            </Col>
            <Col xs={12} md={6} lg={4} class="mb-2">
                <InputGroup>
                    <InputGroupText>Order</InputGroupText>
                    <Input bind:value={options.order} type="select">
                        <option value="ascending">Ascending</option>
                        <option value="descending">Descending</option>
                    </Input>
                </InputGroup>
            </Col>
            <Col xs={12} md={6} lg={4} class="mb-2">
                <InputGroup>
                    <InputGroupText>View mode</InputGroupText>
                    <Input bind:value={pageOptions.view} type="select" aria-label="view mode">
                        <option value="list">List</option>
                        <option value="card">Cards</option>
                        <option value="tiny">Tiny</option>
                        <option value="text">Text Only</option>
                    </Input>
                </InputGroup>
            </Col>
            <Col xs={12} lg={4} class="mb-2">
                <InputGroup>
                    <InputGroupText>Sort By</InputGroupText>
                    <Input bind:value={options.sort} type="select" aria-label="page length">
                        <option value="name">Name</option>
                        <option value="display_name">Display name</option>
                        <option value="id">ID</option>
                        <option value="color">Color</option>
                        <option value="created">Creation date</option>
                        <option value="none">API response order</option>
                    </Input>
                </InputGroup>
            </Col>
            <Col xs={12} md={6} lg={4} class="mb-2">
                {#if pageOptions.view === "text"}
                    <InputGroup>
                        <InputGroupText>Extra Info</InputGroupText>
                        <Input bind:value={options.extra} type="select" aria-label="view mode" >
                            <option value="display_name">Display Name</option>
                            {#if pageOptions.type === "member"}
                            <option value="avatar_url">Avatar Url</option>
                            <option value="webhook_avatar_url">Proxy Avatar Url</option>
                            <option value="pronouns">Pronouns</option>
                            <option value="birthday">Birthday</option>
                            {:else if pageOptions.type === "group"}
                            <option value="icon">Icon Url</option>
                            {/if}
                            <option value="banner">Banner Url</option>
                            <option value="color">Color</option>
                            <option value="created">Created</option>
                        </Input>
                    </InputGroup>
                {:else if pageOptions.type === "member"}
                    <InputGroup>
                        <InputGroupText>Avatar Used</InputGroupText>
                        <Input bind:value={options.pfp} type="select" aria-label="view mode" >
                            <option value="proxy">Proxy (fall back to main)</option>
                            <option value="avatar">Main (fall back to proxy)</option>
                            <option value="proxy_only">Proxy only</option>
                            <option value="avatar_only">Main only</option>
                        </Input>
                    </InputGroup>
                {/if}
            </Col>
        </Row>
        <hr/>
        <Row>
            <Col xs={12} lg={12} class="mb-2">
                <InputGroup class="mb-2">
                    <InputGroupText>Name</InputGroupText>
                    <Input 
                        style="resize: none; overflow: hidden;" 
                        rows={1} type="textarea" 
                        bind:value={options.search.name}
                        placeholder="Search by name..."/>
                </InputGroup> 
            </Col>
        </Row>
        <details>
            <summary class="mb-3"><b>Toggle extra search fields</b></summary>
            <Row>
                <Col xs={12} lg={6} class="mb-2">
                    <InputGroup class="mb-2">
                        <InputGroupText>Display Name</InputGroupText>
                        <Input 
                            style="resize: none; overflow: hidden;" 
                            rows={1} type="textarea" 
                            bind:value={options.search.display_name}
                            placeholder="Search by display name..."/>
                    </InputGroup> 
                </Col>
                <Col xs={12} lg={6} class="mb-2">
                    <InputGroup class="mb-2">
                        <InputGroupText>ID</InputGroupText>
                        <Input 
                            style="resize: none; overflow: hidden;" 
                            rows={1} type="textarea" 
                            bind:value={options.search.id}
                            placeholder="Search by id..."/>
                    </InputGroup> 
                </Col>
                <Col xs={12} lg={6} class="mb-2">
                    <InputGroup class="mb-2">
                        <InputGroupText>Description</InputGroupText>
                        <Input 
                            style="resize: none; overflow: hidden;" 
                            rows={1} type="textarea" 
                            bind:value={options.search.description}
                            placeholder="Search by description..."/>
                    </InputGroup> 
                </Col>
            </Row>
        </details>
    </CardBody>
</Card>