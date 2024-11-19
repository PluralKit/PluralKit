<script lang="ts">
    import { Row, Col, Button, Alert, ListGroup, ListGroupItem, Spinner, TabContent, TabPane, Card, CardBody } from 'sveltestrap';
    import ListPagination from "../common/ListPagination.svelte";
    import twemoji from "twemoji";
    import Svelecte, { addFormatter } from 'svelecte';
    import AwaitHtml from '../common/AwaitHtml.svelte';
    import parseMarkdown from '../../api/parse-markdown';

    import FaFolderOpen from 'svelte-icons/fa/FaFolderOpen.svelte'
    import FaFolderPlus from 'svelte-icons/fa/FaFolderPlus.svelte'
    import FaFolderMinus from 'svelte-icons/fa/FaFolderMinus.svelte'

    import type { Member, Group } from '../../api/types';
    import api from '../../api';
    import type { Writable } from 'svelte/store';
    import { getContext } from 'svelte';

    export let member: Member;
    let loading: boolean = false;
    let err: string;
    export let groupMode: boolean = true;

    $: groups = getContext<Writable<Group[]>>("groups")
    $: members = getContext<Writable<Member[]>>("members")

    let groupsWithMember: Group[];
    let groupsWithoutMember: Group[];
    let groupsWithMemberSelection: any[];
    let groupsWithoutMemberSelection: any[];

    let groupsToBeAdded: any[];
    let groupsToBeRemoved: any[];

    let currentPage = 1;
    let smallPages = true;


    $: groupsWithMember = $groups.filter(group => group.members && group.members.includes(member.uuid)).sort((a, b) => a.name.localeCompare(b.name));;

    $: groupsWithoutMember = $groups.filter(group => group.members && !group.members.includes(member.uuid)).sort((a, b) => a.name.localeCompare(b.name));

    $: groupsWithMemberSelection = groupsWithMember.map(function(group) { return {name: group.name, shortid: group.id, id: group.uuid, members: group.members, display_name: group.display_name}; }).sort((a, b) => a.name.localeCompare(b.name));
    $: groupsWithoutMemberSelection = groupsWithoutMember.map(function(group) { return {name: group.name, shortid: group.id, id: group.uuid, members: group.members, display_name: group.display_name}; }).sort((a, b) => a.name.localeCompare(b.name));

    $: indexOfLastItem = currentPage * 10;
    $: indexOfFirstItem = indexOfLastItem - 10;
    $: pageAmount = Math.ceil(groupsWithMember && groupsWithMember.length / 10);

    $: finalGroupsList = groupsWithMember && groupsWithMember.slice(indexOfFirstItem, indexOfLastItem);

    let settings = JSON.parse(localStorage.getItem('pk-settings'));
    let listGroupElements: any[] = [];
    $: if (settings && settings.appearance.twemoji) {
        if (listGroupElements && listGroupElements.length > 0) {
            listGroupElements.forEach(element => element && twemoji.parse(element), { base: 'https://cdn.jsdelivr.net/gh/twitter/twemoji@14.0.2/assets/' });
        };
    }

    function groupListRenderer(item: any) {
        return `${item.name} (<code>${item.shortid}</code>)`;
    }

    addFormatter({
        'member-list': groupListRenderer
    });

    async function submitAdd() {
        let data = groupsToBeAdded;
        try {
            loading = true;
            await api().members(member.id).groups.add.post({data});
            $groups.forEach(group =>  data.includes(group.uuid) && group.members.push(member.uuid));
            groups.set($groups)
            
            err = null;
            groupsToBeAdded = [];
            loading = false;
        } catch (error) {
            console.log(error);
            err = error.message;
            loading = false;
        }
    }

    async function submitRemove() {
        let data = groupsToBeRemoved;
        try {
            loading = true;
            await api().members(member.id).groups.remove.post({data});
            $groups.forEach(group => {if (data.includes(group.uuid)) group.members = group.members.filter(m => m !== member.uuid)});
            groups.set($groups)

            err = null;
            groupsToBeRemoved = [];
            loading = false;
        } catch (error) {
            console.log(error);
            err = error.message;
            loading = false;
        }
    }

    $: shortGroupList = (list: Group[]) => {
        let str = []
        list.forEach(g => {
            str.push(`[\`${g.id}\`] **${g.display_name ? g.display_name : g.name}**`)
        })

        return str.join("\n")
    }

    $: longGroupList = (list: Group[]) => list.map(g => g.display_name ? g.display_name : g.name).join(", ")

</script>
{#if err}
    <Alert color="danger">{err}</Alert>
{/if}
{#if longGroupList(groupsWithMember).length > 1000}
<Alert color="warning">The total length of the group list is <b>greater than 1000 characters</b>, which means the group list will be cut short when viewed in the bot!</Alert>    
{/if}
<Row>
    <Col xs={12} lg={6} class="text-center mb-3">
        <h5><div class="icon d-inline-block">
            <FaFolderOpen />
        </div>Current Groups
        {#if groupsWithMember && groupsWithMember.length > 0}
            ({groupsWithMember.length} total)
        {/if}
        </h5>
        <TabContent>
            <TabPane tabId="list" tab="List" active={true}>
            <Card>
                <CardBody>
                    {#if groupsWithMember && groupsWithMember.length > 0}
                    <ListPagination bind:currentPage bind:pageAmount bind:smallPages/>
                    <ListGroup>
                        {#each finalGroupsList as group, index (group.id)}
                        <ListGroupItem class="d-flex"><span bind:this={listGroupElements[index]} class="d-flex justify-content-between flex-grow-1"><span><b>{group.name}</b> (<code>{group.id}</code>)</span> <span><AwaitHtml htmlPromise={group.display_name ? parseMarkdown(group.display_name) : Promise.resolve("")} /></span></span></ListGroupItem>
                        {/each}
                    </ListGroup>
                    {:else}
                    <p>This member is not a part of any group.</p>
                    <p>You can add this member to groups in this menu!</p>
                {/if}
                </CardBody>
            </Card>
            </TabPane>
            <TabPane tabId="formatted" tab="Formatted">
                <Card>
                    <CardBody>
                        {#if groupsWithMember && groupsWithMember.length > 0}
                        <p class="mb-0" style="text-align: left;">
                            {#if groupsWithMember.length <= 5}
                                <AwaitHtml htmlPromise={parseMarkdown(shortGroupList(groupsWithMember), { embed: true, parseTimestamps: true })} />
                            {:else}
                                <AwaitHtml htmlPromise={parseMarkdown(longGroupList(groupsWithMember), { embed: true, parseTimestamps: true })} />
                            {/if}
                        </p>
                        {:else}
                            <p>This member is not a part of any group.</p>
                            <p>You can add this member to groups in this menu!</p>
                        {/if}
                    </CardBody>
                </Card>
            </TabPane>
        </TabContent>
    </Col>
    <Col xs={12} lg={6} class="text-center mb-3">
        <h5><div class="icon d-inline-block">
            <FaFolderPlus />
        </div>Add to Groups</h5>
        <Svelecte renderer="member-list" disableHighlight bind:value={groupsToBeAdded} options={groupsWithoutMemberSelection} multiple />
        {#if !loading && groupsToBeAdded && groupsToBeAdded.length > 0}
        <Button class="w-100 mt-2" color="primary" on:click={submitAdd} aria-label="add groups to member">Add</Button>{:else}
        <Button class="w-100 mt-2" color="primary" disabled aria-label="add groups to member">{#if loading}<Spinner size="sm" />{:else}Add{/if}</Button>
        {/if}
        <hr/>
        <h5><div class="icon d-inline-block">
            <FaFolderMinus />
        </div>Remove from Groups</h5>
        <Svelecte renderer="member-list" disableHighlight bind:value={groupsToBeRemoved} options={groupsWithMemberSelection} multiple />
        {#if !loading && groupsToBeRemoved && groupsToBeRemoved.length > 0}
        <Button class="w-100 mt-2" color="primary" on:click={submitRemove} aria-label="remove groups from member">Remove</Button>{:else}
        <Button class="w-100 mt-2" color="primary" disabled aria-label="remove groups from member">{#if loading}<Spinner size="sm" />{:else}Remove{/if}</Button>
        {/if}
    </Col>
</Row>
<Button style="flex: 0" color="secondary" on:click={() => groupMode = false} aria-label="back to member card">Back</Button>