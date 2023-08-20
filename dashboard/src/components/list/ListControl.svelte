<script lang="ts">
import { createEventDispatcher } from 'svelte';
import { Card, CardHeader, CardBody, CardTitle, InputGroupText, InputGroup, Input, Row, Col, Button, Tooltip, Label } from 'sveltestrap';
import FaSearch from 'svelte-icons/fa/FaSearch.svelte'
import FaQuestionCircle from 'svelte-icons/fa/FaQuestionCircle.svelte'
import Svelecte, { addFormatter } from 'svelecte';
import type { Member, Group } from '../../api/types';
import { Link, useParams } from 'svelte-navigator';

import type { ListOptions, List, PageOptions, ShortList } from './types';

export let options: ListOptions;
export let pageOptions: PageOptions;
export let shortGroups: any[] = []
export let shortMembers: any[] = []

let advanced = false;

let itemsPerPageSelection = {
    small: 10,
    default: 25,
    large: 50
}

$: { if (pageOptions.view === "card") itemsPerPageSelection = {
        small: 12,
        default: 24,
        large: 48
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

const dispatch = createEventDispatcher();

function onViewChange(e: any) {
    resetPage();
    if (e.target?.value === 'card') {
        pageOptions.itemsPerPage = 24
    } else if (e.target?.value === 'tiny') {
        pageOptions.itemsPerPage = 36
    } else {
        pageOptions.itemsPerPage = 25
    }
    dispatch("viewChange", e.target.value);
}

let params = useParams();
$: systemId = $params.id;

function groupListRenderer(item: any) {
return `${item.name} (<code>${item.shortid}</code>)`;
}

addFormatter({
    'group-list': groupListRenderer
});

function memberListRenderer(item: any) {
    return `${item.name} (<code>${item.shortid}</code>)`;
}

addFormatter({
    'member-list': memberListRenderer
});

function getRandomizerUrl(): string {
    let str: string;
    if (pageOptions.isPublic) str = `/profile/s/${systemId}/random`
    else str = "/dash/random";
    
    if (pageOptions.type === "group") str += "/g";
    return str;
}

function capitalizeFirstLetter(string: string) {
    return string.charAt(0).toUpperCase() + string.slice(1);
}

function resetPage() {
    pageOptions.currentPage = 1;
}
</script>

<Card class="mb-3">
<CardHeader>
    <CardTitle class="mb-0">
        <Row class="justify-content-between align-items-center ">
            <Col xs={12} md={8} xl={9}>
                <div class="icon d-inline-block">
                    <FaSearch />
                </div> Control {pageOptions.type} list
            </Col>
            {#if !pageOptions.isPublic}
                <Col xs={12} md={4} xl={3} class="mt-2 mt-md-0">
                    <Button class="w-100" color="primary" on:click={() => advanced = !advanced}>Toggle advanced mode</Button>
                </Col>
            {/if}
        </Row>
    </CardTitle>
</CardHeader>
<CardBody>
    <Row>
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
        <Col xs={12} md={6} lg={4} class="mb-2">
            <InputGroup>
                <InputGroupText>Sort by</InputGroupText>
                <Input bind:value={options.sort} type="select">
                    <option value="name">Name</option>
                    <option value="display_name">Display name</option>
                    <option value="id">ID</option>
                    {#if pageOptions.type === 'member'}
                    <option value="pronouns">Pronouns</option>
                    <option value="birthday">Birthday</option>
                    {/if}
                    <option value="color">Color</option>
                    <option value="created">Creation date</option>
                    <option value="none">API response order</option>
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
                <InputGroupText>Only show</InputGroupText>
                <Input bind:value={options.show} type="select" aria-label="view mode" on:change={() => resetPage()}>
                    <option value="all">All {pageOptions.type}s</option>
                    <option value="public">Public {pageOptions.type}s</option>
                    <option value="private">Private {pageOptions.type}s</option>
                </Input>
            </InputGroup>
        </Col>
        <Col xs={12} md={6} lg={4} class="mb-2">
            <InputGroup>
                <InputGroupText>View mode</InputGroupText>
                <Input bind:value={pageOptions.view} type="select" aria-label="view mode" on:change={(e) => onViewChange(e)} >
                    <option value="list">List</option>
                    <option value="card">Cards</option>
                    <option value="tiny">Tiny</option>
                    <option value="text">Text</option>
                </Input>
            </InputGroup>
        </Col>
        <Col xs={12} md={6} lg={4} class="mb-2">
            {#if pageOptions.view === "text"}
                <InputGroup>
                    <InputGroupText>Extra Info</InputGroupText>
                    <Input bind:value={options.extra} type="select" aria-label="view mode" on:change={(e) => onViewChange(e)} >
                        <option value={null}>None</option>
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
            {/if}
        </Col>
    </Row>
    <hr/>
    <CardTitle class="d-flex justify-content-between my-3 h4">
        <div>
            Search {pageOptions.type === "member" ? "members" : "groups"}
        </div>
      <div class="icon d-inline-block" id={`${pageOptions.type}-search-help`}>            
            <FaQuestionCircle />
     </div>
        <Tooltip target={`${pageOptions.type}-search-help`} placement="left" >You can search by multiple fields at the same time.<br/>The toggle controls whether to <b>exclude</b> or <b>include</b> the search term.</Tooltip>
    </CardTitle>
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
                    <InputGroupText>
                        <Input bind:checked={options.searchMode.name} type="switch"/>
                    </InputGroupText>
                </InputGroup> 
            </Col>
        </Row>
    <details>
        <summary><b>Toggle extra search fields</b></summary>
        <Row class="mt-3">
            <Col xs={12} lg={6} class="mb-2">
                <InputGroup class="mb-2">
                    <InputGroupText>Display Name</InputGroupText>
                    <Input 
                        style="resize: none; overflow: hidden;" 
                        rows={1} type="textarea" 
                        bind:value={options.search.display_name} 
                        on:keydown={() => resetPage()} 
                        placeholder="Search by display name..."/>
                    <InputGroupText>
                        <Input bind:checked={options.searchMode.display_name} type="switch"/>
                    </InputGroupText>
                </InputGroup>
            </Col>
            <Col xs={12} lg={6} class="mb-2">
                <InputGroup class="mb-2">
                    <InputGroupText>ID</InputGroupText>
                    <Input 
                        style="resize: none; overflow: hidden;" 
                        rows={1} type="textarea" 
                        bind:value={options.search.id} 
                        on:keydown={() => resetPage()} 
                        placeholder="Search by ID..."/>
                        <InputGroupText>
                            <Input bind:checked={options.searchMode.id} type="switch"/>
                        </InputGroupText>
                </InputGroup>
            </Col>
            {#if pageOptions.type === 'member'}
            <Col xs={12} lg={6} class="mb-2">
                <InputGroup class="mb-2">
                    <InputGroupText>Pronouns</InputGroupText>
                    <Input 
                        style="resize: none; overflow: hidden;" 
                        rows={1} type="textarea" 
                        bind:value={options.search.pronouns} 
                        on:keydown={() => resetPage()} 
                        placeholder="Search by pronouns..."/>
                        <InputGroupText>
                            <Input bind:checked={options.searchMode.pronouns} type="switch"/>
                        </InputGroupText>
                </InputGroup>
            </Col>
            {/if}
            <Col xs={12} lg={6} class="mb-2">
                <InputGroup class="mb-2">
                    <InputGroupText>Description</InputGroupText>
                    <Input 
                        style="resize: none;" 
                        rows={1} type="textarea" 
                        bind:value={options.search.description} 
                        on:keydown={() => resetPage()} 
                        placeholder="Search by description..."/>
                        <InputGroupText>
                            <Input bind:checked={options.searchMode.description} type="switch"/>
                        </InputGroupText>
                </InputGroup>
            </Col>
        </Row>
        </details>

{#if advanced}
        <hr/>
        <CardTitle class="d-flex justify-content-between my-3 h4">
            <div>
                Filter by {pageOptions.type === "member" ? "groups" : "members"}
            </div>
          <div class="icon d-inline-block" id={`${pageOptions.type}-groups-help`}>            
                <FaQuestionCircle />
         </div>
            <Tooltip target={`${pageOptions.type}-groups-help`} placement="left" >You can select what groups/members to <b>include and exclude</b> here. Exact? means only items with <b>all</b> selected members/groups will be included. Excluded items take priority over included</Tooltip>
        </CardTitle>
            <Row>
                <p><b>Include</b> {pageOptions.type === 'group' ? "groups with the following members" : "members in the following groups"}</p>
                <Col xs={12} md={7} lg={9}  class="mb-2">
                    {#if pageOptions.type === "member"}
                    <Svelecte disableHighlight renderer="group-list" valueAsObject bind:value={options.groups.include.list} options={shortGroups} multiple style="margin-bottom: 0.5rem" placeholder="Include..." />
                    {:else if pageOptions.type === "group"}
                    <Svelecte disableHighlight renderer="member-list" valueAsObject bind:value={options.groups.include.list} options={shortMembers} multiple style="margin-bottom: 0.5rem" placeholder="Include..." />
                    {/if}
                </Col>
                <Col xs={12} md={5} lg={3} class="mb-2">
                    <InputGroup>
                        <InputGroupText class="w-75">Exact?</InputGroupText>
                        <InputGroupText class="w-25 bg-body d-flex justify-content-center">
                            <Input bind:checked={options.groups.include.exact} type="switch"/>
                        </InputGroupText>
                    </InputGroup>
                </Col>
            </Row>
            <Row>
                <p><b>Exclude</b> {pageOptions.type === 'group' ? "groups with the following members" : "members in the following groups"}</p>
                <Col xs={12} md={7} lg={9}  class="mb-2">
                    {#if pageOptions.type === "member"}
                    <Svelecte disableHighlight renderer="group-list" valueAsObject bind:value={options.groups.exclude.list} options={shortGroups} multiple style="margin-bottom: 0.5rem" placeholder="Exclude..." />
                    {:else if pageOptions.type === "group"}
                    <Svelecte disableHighlight renderer="member-list" valueAsObject bind:value={options.groups.exclude.list} options={shortMembers} multiple style="margin-bottom: 0.5rem" placeholder="Exclude..." />
                    {/if}
                </Col>
                <Col xs={12} md={5} lg={3} class="mb-2">
                    <InputGroup>
                        <InputGroupText class="w-75">Exact?</InputGroupText>
                        <InputGroupText class="w-25 bg-body d-flex justify-content-center">
                            <Input bind:checked={options.groups.exclude.exact} type="switch"/>
                        </InputGroupText>
                    </InputGroup>
                </Col>
            </Row>
            <hr/>
            <CardTitle class="d-flex justify-content-between my-3 h4">
                <div>
                    Filter by fields
                </div>
                <div class="icon d-inline-block" id={`${pageOptions.type}-filters-help`}>            
                    <FaQuestionCircle />
                </div>
                <Tooltip target={`${pageOptions.type}-filters-help`} placement="left" >You can filter out items based on whether a certain field has been filled out or not here.</Tooltip>
            </CardTitle>
            <Row class="mt-3">
                <Col xs={12} md={6} lg={4} class="mb-2">
                    <InputGroup>
                        <InputGroupText>Display name</InputGroupText>
                        <Input type="select" bind:value={options.filter.display_name} on:change={() => resetPage()}>
                            <option value="all">All</option>
                            <option value="include">With display name</option>
                            <option value="exclude">Without display name</option>
                        </Input>
                    </InputGroup>
                </Col>
                <Col xs={12} md={6} lg={4} class="mb-2">
                    <InputGroup>
                        <InputGroupText>Description</InputGroupText>
                        <Input type="select" bind:value={options.filter.description} on:change={() => resetPage()}>
                            <option value="all">All</option>
                            <option value="include">With description</option>
                            <option value="exclude">Without description</option>
                        </Input>
                    </InputGroup>
                </Col>
                {#if pageOptions.type === 'member'}
                <Col xs={12} md={6} lg={4} class="mb-2">
                    <InputGroup>
                        <InputGroupText>Proxy tags</InputGroupText>
                        <Input type="select" bind:value={options.filterArray.proxy_tags} on:change={() => resetPage()}>
                            <option value="all">All</option>
                            <option value="include">With proxy tags</option>
                            <option value="exclude">Without proxy tags</option>
                        </Input>
                    </InputGroup>
                </Col>
                <Col xs={12} md={6} lg={4} class="mb-2">
                    <InputGroup>
                        <InputGroupText>Avatar</InputGroupText>
                        <Input type="select" bind:value={options.filter.avatar_url} on:change={() => resetPage()}>
                            <option value="all">All</option>
                            <option value="include">With avatar</option>
                            <option value="exclude">Without avatar</option>
                        </Input>
                    </InputGroup>
                </Col>
                <Col xs={12} md={6} lg={4} class="mb-2">
                    <InputGroup>
                        <InputGroupText>Birthday</InputGroupText>
                        <Input type="select" bind:value={options.filter.birthday} on:change={() => resetPage()}>
                            <option value="all">All</option>
                            <option value="include">With birthday</option>
                            <option value="exclude">Without birthday</option>
                        </Input>
                    </InputGroup>
                </Col>
                <Col xs={12} md={6} lg={4} class="mb-2">
                    <InputGroup>
                        <InputGroupText>Pronouns</InputGroupText>
                        <Input type="select" bind:value={options.filter.pronouns} on:change={() => resetPage()}>
                            <option value="all">All</option>
                            <option value="include">With pronouns</option>
                            <option value="exclude">Without pronouns</option>
                        </Input>
                    </InputGroup>
                </Col>
                {:else}
                    <Col xs={12} md={6} lg={4} class="mb-2">
                        <InputGroup>
                            <InputGroupText>Icon</InputGroupText>
                            <Input type="select" bind:value={options.filter.icon} on:change={() => resetPage()}>
                                <option value="all">All</option>
                                <option value="include">With icon</option>
                                <option value="exclude">Without icon</option>
                            </Input>
                        </InputGroup>
                    </Col>
                {/if}
                <Col xs={12} md={6} lg={4} class="mb-2">
                    <InputGroup>
                        <InputGroupText>Banner</InputGroupText>
                        <Input type="select" bind:value={options.filter.banner} on:change={() => resetPage()}>
                            <option value="all">All</option>
                            <option value="include">With banner</option>
                            <option value="exclude">Without banner</option>
                        </Input>
                    </InputGroup>
                </Col>
                <Col xs={12} md={6} lg={4} class="mb-2">
                    <InputGroup>
                        <InputGroupText>Color</InputGroupText>
                        <Input type="select" bind:value={options.filter.color} on:change={() => resetPage()}>
                            <option value="all">All</option>
                            <option value="include">With color</option>
                            <option value="exclude">Without color</option>
                        </Input>
                    </InputGroup>
                </Col>
                {#if pageOptions.type === 'member'}
                <Col xs={12} md={6} lg={4} class="mb-2">
                    <InputGroup>
                        <InputGroupText>Groups</InputGroupText>
                        <Input type="select" bind:value={options.groups.filter} on:change={() => resetPage()}>
                            <option value="all">All</option>
                            <option value="include">With groups</option>
                            <option value="exclude">Without groups</option>
                        </Input>
                    </InputGroup>
                </Col>
                {:else}
                <Col xs={12} md={6} lg={4} class="mb-2">
                    <InputGroup>
                        <InputGroupText>Members</InputGroupText>
                        <Input type="select" bind:value={options.groups.filter} on:change={() => resetPage()}>
                            <option value="all">All</option>
                            <option value="include">With members</option>
                            <option value="exclude">Without members</option>
                        </Input>
                    </InputGroup>
                </Col>
                {/if}
            </Row>
        {/if}
        <hr/>
        <Row>
            <Col></Col>
            <Col xs={12} md={4} lg={3} class="mb-2">
                <Link to={getRandomizerUrl()}><Button class="w-100" color="secondary" tabindex={-1} aria-label={`randomize ${pageOptions.type}s`}>Random {capitalizeFirstLetter(pageOptions.type)}</Button></Link>
            </Col>
        </Row>
    </CardBody>
</Card>