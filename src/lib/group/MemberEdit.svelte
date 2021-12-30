<script lang="ts">
    import type Group from "../../api/group";
    import type Member from "../../api/member";
    import { Row, Col, Button, Alert, ListGroup, ListGroupItem } from 'sveltestrap';
    import { createEventDispatcher } from 'svelte';
    import PKAPI from '../../api';
    import ListPagination from "../ListPagination.svelte";
    import twemoji from "twemoji";
    import FaUserPlus from 'svelte-icons/fa/FaUserPlus.svelte'
    import FaUserFriends from 'svelte-icons/fa/FaUserFriends.svelte'
    import FaUserMinus from 'svelte-icons/fa/FaUserMinus.svelte'
    import Svelecte, { addFormatter } from 'svelecte';

    export let loading: boolean;
    export let group: Group;
    export let memberMode: boolean = true;
    export let members: Member[];

    let membersInGroup: Member[];
    let membersNotInGroup: Member[];
    let membersInGroupSelection: any[];
    let membersNotInGroupSelection: any[];

    let membersToBeAdded: any[];
    let membersToBeRemoved: any[];

    let currentPage: number = 1;

    let smallPages = true;

    if (group.members) {
        membersInGroup = members.filter(member => group.members.includes(member.uuid));
        membersInGroup = membersInGroup.sort((a, b) => a.name.localeCompare(b.name));

        membersNotInGroup = members.filter(member => !group.members.includes(member.uuid));
        membersNotInGroup = membersNotInGroup.sort((a, b) => a.name.localeCompare(b.name));

        membersInGroupSelection = membersInGroup.map(function(member) { return {name: member.name, shortid: member.id, id: member.uuid, display_name: member.display_name}; }).sort((a, b) => a.name.localeCompare(b.name));
        membersNotInGroupSelection = membersNotInGroup.map(function(member) { return {name: member.name, shortid: member.id, id: member.uuid, display_name: member.display_name}; }).sort((a, b) => a.name.localeCompare(b.name));
    }

    $: indexOfLastItem = currentPage * 10;
    $: indexOfFirstItem = indexOfLastItem - 10;
    $: pageAmount = Math.ceil(membersInGroup.length / 10);

    $: finalMemberList = membersInGroup.slice(indexOfFirstItem, indexOfLastItem);

    let settings = JSON.parse(localStorage.getItem('pk-settings'));
    let listGroupElements: any[] = [];
    $: if (settings && settings.appearance.twemoji) {
        if (listGroupElements && listGroupElements.length > 0) {
            listGroupElements.forEach(element => element && twemoji.parse(element));
        };
    }

function memberListRenderer(item: any) {
    return `${item.name} (<code>${item.shortid}</code>)`;
  }

  addFormatter({
    'member-list': memberListRenderer
  });

</script>

<Row>
    <Col xs={12} lg={6} class="text-center mb-3">
        <h5><div class="icon d-inline-block">
            <FaUserFriends />
        </div>Current Members</h5>
        <ListPagination bind:currentPage bind:pageAmount bind:smallPages/>
        {#if finalMemberList.length > 0}
        <ListGroup>
            {#each finalMemberList as member, index (member.id)}
            <ListGroupItem class="d-flex"><span bind:this={listGroupElements[index]} class="d-flex justify-content-between flex-grow-1"><span><b>{member.name}</b> (<code>{member.id}</code>)</span> <span>{member.display_name ? `${member.display_name}` : ""}</span></span></ListGroupItem>
            {/each}
        </ListGroup>
        {:else}
        <p>There are no members in this group yet.</p>
        <p>You can add some in this menu!</p>
        {/if}
    </Col>
    <Col xs={12} lg={6} class="text-center mb-3">
        <h5><div class="icon d-inline-block">
            <FaUserPlus />
        </div>Add Members</h5>
        <Svelecte renderer="member-list" disableHighlight bind:value={membersToBeAdded} options={membersNotInGroupSelection} multiple/>
        <p>(this is nonfunctional at the moment)</p>
        <hr/>
        <h5><div class="icon d-inline-block">
            <FaUserMinus />
        </div>Remove Members</h5>
        <Svelecte renderer="member-list" disableHighlight bind:value={membersToBeRemoved} options={membersInGroupSelection} multiple/>
        <p>(this is ALSO nonfunctional)</p>
    </Col>
</Row>
<Button style="flex: 0" color="secondary" on:click={() => memberMode = false}>Back</Button>