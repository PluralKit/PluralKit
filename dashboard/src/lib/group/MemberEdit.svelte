<script lang="ts">
    import { Row, Col, Button, Alert, ListGroup, ListGroupItem, Spinner } from 'sveltestrap';
    import ListPagination from "../ListPagination.svelte";
    import twemoji from "twemoji";
    import FaUserPlus from 'svelte-icons/fa/FaUserPlus.svelte'
    import FaUserFriends from 'svelte-icons/fa/FaUserFriends.svelte'
    import FaUserMinus from 'svelte-icons/fa/FaUserMinus.svelte'
    import Svelecte, { addFormatter } from 'svelecte';

    import { Group, Member } from '../../api/types';
    import api from '../../api';

    let loading: boolean = false;
    let err: string;
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

    updateMemberList();

    function updateMemberList() {
        membersInGroup = members.filter(member => group.members.includes(member.uuid));
        membersInGroup = membersInGroup.sort((a, b) => a.name.localeCompare(b.name));

        membersNotInGroup = members.filter(member => !group.members.includes(member.uuid));
        membersNotInGroup = membersNotInGroup.sort((a, b) => a.name.localeCompare(b.name));

        membersInGroupSelection = membersInGroup.map(function(member) { return {name: member.name, shortid: member.id, id: member.uuid, display_name: member.display_name}; }).sort((a, b) => a.name.localeCompare(b.name));
        membersNotInGroupSelection = membersNotInGroup.map(function(member) { return {name: member.name, shortid: member.id, id: member.uuid, display_name: member.display_name}; }).sort((a, b) => a.name.localeCompare(b.name));
    }

    $: indexOfLastItem = currentPage * 10;
    $: indexOfFirstItem = indexOfLastItem - 10;
    $: pageAmount = Math.ceil(membersInGroup && membersInGroup.length / 10);

    $: finalMemberList = membersInGroup && membersInGroup.slice(indexOfFirstItem, indexOfLastItem);

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
    
    async function submitAdd() {
        let data = membersToBeAdded;
        try {
            loading = true;
            await api().groups(group.id).members.add.post({data});
            data.forEach(member => group.members.push(member));
            updateMemberList();
            err = null;
            membersToBeAdded = [];
            loading = false;
        } catch (error) {
            console.log(error);
            err = error.message;
            loading = false;
        }
    }

    async function submitRemove() {
        let data = membersToBeRemoved;
        try {
            loading = true;
            await api().groups(group.id).members.remove.post({data});
            group.members = group.members.filter(m => !data.includes(m));
            updateMemberList();
            err = null;
            membersToBeRemoved = [];
            loading = false;
        } catch (error) {
            console.log(error);
            err = error.message;
            loading = false;
        }
    }

</script>
{#if err}
    <Alert color="danger">{err}</Alert>
{/if}
<Row>
    <Col xs={12} lg={6} class="text-center mb-3">
        <h5><div class="icon d-inline-block">
            <FaUserFriends />
        </div>Current Members</h5>
        <ListPagination bind:currentPage bind:pageAmount bind:smallPages/>
        {#if finalMemberList && finalMemberList.length > 0}
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
        {#if !loading && membersToBeAdded && membersToBeAdded.length > 0}
        <Button class="w-100 mt-2" color="primary" on:click={submitAdd} aria-label="add members">Add</Button>{:else}
        <Button class="w-100 mt-2" color="primary" disabled aria-label="add members">{#if loading}<Spinner size="sm" />{:else}Add{/if}</Button>
        {/if}
        <hr/>
        <h5><div class="icon d-inline-block">
            <FaUserMinus />
        </div>Remove Members</h5>
        <Svelecte renderer="member-list" disableHighlight bind:value={membersToBeRemoved} options={membersInGroupSelection} multiple/>
        {#if !loading && membersToBeRemoved && membersToBeRemoved.length > 0}
        <Button class="w-100 mt-2" color="primary" on:click={submitRemove} aria-label="remove members">Remove</Button>{:else}
        <Button class="w-100 mt-2" color="primary" disabled aria-label="remove members">{#if loading}<Spinner size="sm" />{:else}Remove{/if}</Button>
        {/if}
    </Col>
</Row>
<Button style="flex: 0" color="secondary" on:click={() => memberMode = false} aria-label="back to group card">Back</Button>