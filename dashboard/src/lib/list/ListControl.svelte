<script lang="ts">
import { Card, CardHeader, CardBody, CardTitle, Alert, Accordion, AccordionItem, InputGroupText, InputGroup, Input, Row, Col, Spinner, Button, Tooltip, Label } from 'sveltestrap';
import FaSearch from 'svelte-icons/fa/FaSearch.svelte'
import Svelecte, { addFormatter } from 'svelecte';
import { Member, Group } from '../../api/types';
import { Link, useParams } from 'svelte-navigator';
import moment from 'moment';

export let list: Member[] | Group[] = [];

export let itemType: string;
export let memberList: any = [];
export let groups: Group[] = [];
export let groupList: any = [];

export let searchBy = "name";
export let searchValue: string;
export let itemsPerPageValue: string;

let sortBy = "name";
let sortOrder = "ascending";
let privacyFilter = "all";
let groupSearchMode = "include";
let selectedGroups = [];

export let currentPage: number;
export let isPublic: boolean;

let params = useParams();
$: systemId = $params.id;

$: {searchValue; privacyFilter; currentPage = 1};

// converting list to any[] avoids a "this expression is not calleable" error
$: searchedList = (list as any[]).filter((item: Member | Group) => {
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
    let alphabeticalList = filteredList.sort((a, b) => a.name.localeCompare(b.name));
    switch (sortBy) {
        case "name": sortedList = alphabeticalList;
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
        case "avatar": sortedList = alphabeticalList.sort((a, b) => {
                if (a.icon === null || a.avatar_url === null) {
                    return 1;
                };
                if (b.icon === null || b.avatar_url === null) {
                    return -1;
                };
                if (a.icon === b.icon || a.avatar_url === b.avatar_url) {
                    return 0;
                }
            });
        break;
        case "color": sortedList = alphabeticalList.sort((a, b) => {
                if (a.color === null) {
                    return 1;
                };
                if (b.color === null) {
                    return -1;
                };
                if (a.color === b.color) {
                    return 0;
                }
            });
        break;
        case "birthday": sortedList = alphabeticalList.sort((a, b) => {
            if (a.birthday === null) {
                return 1;
            }
            if (b.birthday === null) {
                return -1;
            }
            let aBirthday = moment(a.birthday.slice(5, a.birthday.length), "MM-DD", true);
            let bBirthday = moment(b.birthday.slice(5, b.birthday.length), "MM-DD", true);
            if (aBirthday.isBefore(bBirthday)) {
                return -1;
            }
            if (aBirthday.isAfter(bBirthday)) {
                return 1;
            }
            if (aBirthday === bBirthday) {
                return 0;
            }
        });
        break;
        case "pronouns": sortedList = alphabeticalList.sort((a, b) => {
            if (a.pronouns === null) {
                    return 1;
                };
                if (b.pronouns === null) {
                    return -1;
                };
                return 0;
        })
        break;
        default: sortedList = filteredList.sort((a, b) => a.name.localeCompare(b.name));
        break;
    }
}

let memberFilteredList = [];
$: memberFilteredList = sortedList.filter((item: Member | Group): boolean => { 
    if (itemType === "member") {
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
    } else if (itemType === "group") {
        let group = (item as Group);

        if (groupSearchMode === "none") {
            if (group.members && group.members.length > 0) return false;
        }

        if (selectedGroups.length < 1) return true;

        switch (groupSearchMode) {
            case "include": if (group.members && selectedGroups.some(member => group.members.includes(member.id))) return true;
            break;
            case "exclude": if (group.members && selectedGroups.every(member => !group.members.includes(member.id))) return true;
            break;
            case "match": if (group.members && selectedGroups.every(member => group.members.includes(member.id))) return true;
            break;
            default: return true;
        }
    }
    return false;
})

export let finalList = [];
$:{sortOrder; if (sortOrder === "descending") finalList = memberFilteredList.reverse(); else finalList = memberFilteredList;}

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
    if (isPublic) str = `/profile/s/${systemId}/random`
    else str = "/dash/random";
    
    if (itemType === "group") str += "/g";
    return str;
}

function capitalizeFirstLetter(string: string) {
    return string.charAt(0).toUpperCase() + string.slice(1);
}
</script>

<Card class="mb-3">
<CardHeader>
    <CardTitle>
        <CardTitle style="margin-top: 8px; outline: none;">
            <div class="icon d-inline-block">
                <FaSearch />
            </div> Search {itemType === "member" ? "members" : "groups"}
        </CardTitle>
    </CardTitle>
</CardHeader>
<CardBody>
    <Row>
        <Col xs={12} lg={3} class="mb-2">
            <InputGroup>
                <InputGroupText>Page length</InputGroupText>
                <Input bind:value={itemsPerPageValue} type="select" aria-label="page length">
                    <option>10</option>
                    <option>25</option>
                    <option>50</option>
                </Input>
            </InputGroup>
        </Col>
        <Col xs={12} lg={3} class="mb-2">
            <InputGroup>
                <InputGroupText>Search by</InputGroupText>
                <Input bind:value={searchBy} type="select" aria-label="search by">
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
                <Input bind:value={sortBy} type="select" aria-label="sort by">
                    <option>name</option>
                    <option>display name</option>
                    {#if !isPublic}<option>creation date</option>{/if}
                    <option>ID</option>
                    <option>avatar</option>
                    <option>color</option>
                    {#if itemType === "member"}<option>birthday</option>{/if}
                    {#if itemType === "member"}<option>pronouns</option>{/if}
                </Input>
            </InputGroup>
        </Col>
        <Col xs={12} lg={3} class="mb-2">
            <InputGroup>
                <InputGroupText>Sort order</InputGroupText>
                <Input bind:value={sortOrder} type="select" aria-label="sort order">
                    <option>ascending</option>
                    <option>descending</option>
                </Input>
            </InputGroup>
        </Col>
        {#if !isPublic}
        <Col xs={12} lg={3} class="mb-2">
            <InputGroup>
                <InputGroupText>Only show</InputGroupText>
                <Input bind:value={privacyFilter} type="select" aria-label="only show">
                    <option>all</option>
                    <option>public</option>
                    <option>private</option>
                </Input>
            </InputGroup>
        </Col>
        {/if}
        <Col xs={12} lg={3} class="mb-2">
            <Link to={getRandomizerUrl()}><Button class="w-100" color="secondary" tabindex={-1} aria-label={`randomize ${itemType}s`}>Random {capitalizeFirstLetter(itemType)}</Button></Link>
        </Col>
    </Row>
    {#if !isPublic}
    <hr/>
    <Label>Filter {itemType === "member" ? "member" : "group"} by {itemType === "member" ? "group" : "member"}</Label>
    {#if itemType === "member"}
    <Svelecte disableHighlight renderer="group-list" valueAsObject bind:value={selectedGroups} options={groupList} multiple style="margin-bottom: 0.5rem" />
    {:else if itemType === "group"}
    <Svelecte disableHighlight renderer="member-list" valueAsObject bind:value={selectedGroups} options={memberList} multiple style="margin-bottom: 0.5rem" />
    {/if}

    <div class="filter-mode-group">
    <span class="filter-mode-label" id="m-include" on:click={() => groupSearchMode = "include"} on:keyup={e => e.key === "Enter" ? groupSearchMode = "include" : ""} tabindex={0}>{@html groupSearchMode === "include" ? "<b>include</b>" : "include"}</span>
     | <span class="filter-mode-label" id="m-exclude" on:click={() => groupSearchMode = "exclude"} on:keyup={e => e.key === "Enter" ? groupSearchMode = "exclude" : ""} tabindex={0}>{@html groupSearchMode === "exclude" ? "<b>exclude</b>" : "exclude"}</span> 
     | <span class="filter-mode-label" id="m-match" on:click={() => groupSearchMode = "match"} on:keyup={e => e.key === "Enter" ? groupSearchMode = "match" : ""} tabindex={0}>{@html groupSearchMode === "match" ? "<b>exact match</b>" : "exact match"}</span>
     | <span class="filter-mode-label" id="m-none" on:click={() => groupSearchMode = "none"} on:keyup={e => e.key === "Enter" ? groupSearchMode = "none" : ""} tabindex={0}>{@html groupSearchMode === "none" ? "<b>none</b>" : "none"}</span>
    </div>
     <Tooltip placement="bottom" target="m-include">Includes every member who's a part of any of the groups.</Tooltip>
    <Tooltip placement="bottom" target="m-exclude">Excludes every member who's a part of any of the groups, the opposite of include.</Tooltip>
    <Tooltip placement="bottom" target="m-match">Only includes members who are a part of every group.</Tooltip>
    <Tooltip placement="bottom" target="m-none">Only includes members that are in no groups.</Tooltip>
    {/if}
</CardBody>
</Card>

<style>
    .filter-mode-label {
        cursor: pointer;
    }

    .filter-mode-group {
        line-height: 1.5em;
        padding:0.375rem 0;
        display: inline-block;
        margin-bottom: 0.25em;
    }
</style>