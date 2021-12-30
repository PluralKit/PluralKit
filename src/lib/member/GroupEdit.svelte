<script lang="ts">
    import type Group from "../../api/group";
    import type Member from "../../api/member";
    import { Row, Col, Button, Alert, ListGroup, ListGroupItem } from 'sveltestrap';
    import { createEventDispatcher } from 'svelte';
    import PKAPI from '../../api';
    import ListPagination from "../ListPagination.svelte";
    import twemoji from "twemoji";
    import Svelecte, { addFormatter } from 'svelecte';
    import { toHTML } from 'discord-markdown';

    import FaFolderOpen from 'svelte-icons/fa/FaFolderOpen.svelte'
    import FaFolderPlus from 'svelte-icons/fa/FaFolderPlus.svelte'
    import FaFolderMinus from 'svelte-icons/fa/FaFolderMinus.svelte'

    export let member: Member;
    export let groups: Group[] = [];
    let loading: boolean = false;
    export let groupMode: boolean = true;

    let groupsWithMember: Group[];
    let groupsWithoutMember: Group[];
    let groupsWithMemberSelection: any[];
    let groupsWithoutMemberSelection: any[];

    let groupsToBeAdded: any[];
    let groupsToBeRemoved: any[];

    let currentPage = 1;
    let smallPages = true;

    if (groups) {
        groupsWithMember = groups.filter(group => group.members.includes(member.uuid));
        groupsWithMember.sort((a, b) => a.name.localeCompare(b.name));

        groupsWithoutMember = groups.filter(group => !group.members.includes(member.uuid));
        groupsWithoutMember.sort((a, b) => a.name.localeCompare(b.name));

        groupsWithMemberSelection = groupsWithMember.map(function(group) { return {name: group.name, shortid: group.id, id: group.uuid, members: group.members, display_name: group.display_name}; }).sort((a, b) => a.name.localeCompare(b.name));
        groupsWithoutMemberSelection = groupsWithoutMember.map(function(group) { return {name: group.name, shortid: group.id, id: group.uuid, members: group.members, display_name: group.display_name}; }).sort((a, b) => a.name.localeCompare(b.name));
    }

    $: indexOfLastItem = currentPage * 10;
    $: indexOfFirstItem = indexOfLastItem - 10;
    $: pageAmount = Math.ceil(groupsWithMember.length / 10);

    $: finalGroupsList = groupsWithMember.slice(indexOfFirstItem, indexOfLastItem);

    let settings = JSON.parse(localStorage.getItem('pk-settings'));
    let listGroupElements: any[] = [];
    $: if (settings && settings.appearance.twemoji) {
        if (listGroupElements && listGroupElements.length > 0) {
            listGroupElements.forEach(element => element && twemoji.parse(element));
        };
    }

    function groupListRenderer(item: any) {
        return `${item.name} (<code>${item.shortid}</code>)`;
    }

    addFormatter({
        'member-list': groupListRenderer
    });

</script>

<Row>
    <Col xs={12} lg={6} class="text-center mb-3">
        <h5><div class="icon d-inline-block">
            <FaFolderOpen />
        </div>Current Groups</h5>
        <ListPagination bind:currentPage bind:pageAmount bind:smallPages/>
        {#if finalGroupsList.length > 0}
        <ListGroup>
            {#each finalGroupsList as group, index (group.id)}
            <ListGroupItem class="d-flex"><span bind:this={listGroupElements[index]} class="d-flex justify-content-between flex-grow-1"><span><b>{group.name}</b> (<code>{group.id}</code>)</span> <span>{@html group.display_name ? `${toHTML(group.display_name)}` : ""}</span></span></ListGroupItem>
            {/each}
        </ListGroup>
        {:else}
        <p>This member is inside no groups.</p>
        <p>You can add groups in this menu!</p>
        {/if}
    </Col>
    <Col xs={12} lg={6} class="text-center mb-3">
        <h5><div class="icon d-inline-block">
            <FaFolderPlus />
        </div>Add to Groups</h5>
        <Svelecte renderer="member-list" disableHighlight bind:value={groupsToBeAdded} options={groupsWithoutMemberSelection} multiple/>
        <p>(this is nonfunctional at the moment)</p>
        <hr/>
        <h5><div class="icon d-inline-block">
            <FaFolderMinus />
        </div>Remove from Groups</h5>
        <Svelecte renderer="member-list" disableHighlight bind:value={groupsToBeRemoved} options={groupsWithMemberSelection} multiple/>
        <p>(this is ALSO nonfunctional)</p>
    </Col>
</Row>
<Button style="flex: 0" color="secondary" on:click={() => groupMode = false}>Back</Button>