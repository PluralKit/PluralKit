<script lang="ts">
    import { Row, Col, Input, Button, Label, Alert, Spinner, Modal, ModalHeader, ModalBody } from 'sveltestrap';
    import { createEventDispatcher, tick } from 'svelte';
    import type { Group } from '../../api/types';
    import api from '../../api';
    import { autoresize } from 'svelte-textarea-autoresize';

    const descriptions: string[] = JSON.parse(localStorage.getItem("pk-config"))?.description_templates;

    let loading: boolean = false;
    let success = false;

    export let group: Group;
    export let editMode: boolean;

    let err: string[] = [];

    let input: Group = group;

    const dispatch = createEventDispatcher();

    function deletion() {
        dispatch('deletion', group.id);
    }

    function update(group: Group) {
        dispatch('update', group);
    }

    async function submit() {
        let data = input;
        err = [];
        success = false;

        if (data.color && !/^#?[A-Fa-f0-9]{6}$/.test(input.color)) {
            err.push(`"${data.color}" is not a valid color, the color must be a 6-digit hex code. (example: #ff0000)`);
        } else if (data.color) {
            if (data.color.startsWith("#")) {
                data.color = input.color.slice(1, input.color.length);
            }
        }

        // trim all string fields
        Object.keys(data).forEach(k => data[k] = typeof data[k] == "string" ? data[k].trim() : data[k]);

        err = err;
        if (err.length > 0) return;

        loading = true;
        try {
            let res = await api().groups(group.id).patch({data});
            update({...group, ...res});
            err = [];
            editMode = false;
            return;
        } catch (error) {
            console.log(error);
            err.push(error.message);
            err = err;
            loading = false;
        }
    }

    let deleteOpen: boolean = false;
    const toggleDeleteModal = () => deleteOpen = !deleteOpen;

    let deleteInput: string;
    let deleteErr: string;

    async function submitDelete() {
        deleteErr = null;

        if (!deleteInput) {
            deleteErr = "Please type out the group ID.";
            return;
        }

        if (deleteInput.trim().toLowerCase() !== group.id) {
            deleteErr = "This group's ID does not match the provided ID.";
            return;
        }
        loading = true;
        try {
            await api().groups(group.id).delete();
            deleteErr = null;
            toggleDeleteModal();
            loading = false;
            deletion();
        } catch (error) {
            loading = false;
        }
    }

    async function focus(el) {
        await tick();
        el.focus();
    }
</script>

{#each err as error}
<Alert fade={false} color="danger">{@html error}</Alert>
{/each}
{#if success}
<Alert fade={false} color="success">Group information updated!</Alert>
{/if}
<Row>
    <Col xs={12} lg={4} class="mb-2">
        <Label>Name:</Label>
        <Input bind:value={input.name} maxlength={100} type="text" placeholder={group.name} aria-label="group name" />
    </Col>
    <Col xs={12} lg={4} class="mb-2">
        <Label>Display name:</Label>
        <textarea class="form-control" style="resize: none; height: 1em" bind:value={input.display_name} maxlength={100} type="text" placeholder={group.display_name} aria-label="group display name"/>
    </Col>
    <Col xs={12} lg={4} class="mb-2">
        <Label>Color:</Label>
        <Input bind:value={input.color} type="text" placeholder={group.color} aria-label="group color"/>
    </Col>
    <Col xs={12} lg={4} class="mb-2">
        <Label>Icon url:</Label>
        <Input bind:value={input.icon} maxlength={256} type="url" placeholder={group.icon} aria-label="group icon url"/>
    </Col>
    <Col xs={12} lg={4} class="mb-2">
        <Label>Banner url:</Label>
        <Input bind:value={input.banner} maxlength={256} type="url" placeholder={group.banner} aria-label="group banner url"/>
    </Col>
</Row>
<div class="my-2">
    <b>Description:</b><br />
    {#if descriptions.length > 0 && descriptions[0].trim() != ""}
    <Button size="sm" class="my-2" color="primary" on:click={() => input.description = descriptions[0]} aria-label="use template 1">Template 1</Button>
    {/if}
    {#if descriptions.length > 1 && descriptions[1].trim() != ""}
    <Button size="sm" class="my-2" color="primary" on:click={() => input.description = descriptions[1]} aria-label="use template 2">Template 2</Button>
    {/if}
    {#if descriptions.length > 2 && descriptions[2].trim() != ""}
    <Button size="sm" class="my-2" color="primary" on:click={() => input.description = descriptions[2]} aria-label="use template 3">Template 3</Button>
    {/if}
    <br>
    <textarea class="form-control" bind:value={input.description} maxlength={1000} use:autoresize placeholder={group.description} aria-label="group description"/>
</div>
<span class="text-center d-block mb-2 mb-lg-1" >({input.description ? input.description.length : 0} / 1000 characters)</span>
{#if !loading}<Button style="flex: 0" color="primary" on:click={submit} aria-label="submit edits">Submit</Button> <Button style="flex: 0" color="secondary" on:click={() => editMode = false} aria-label="cancel edits">Back</Button><Button style="flex: 0; float: right;" color="danger" on:click={toggleDeleteModal} aria-label="delete group">Delete</Button>
{:else}<Button style="flex: 0" color="primary" disabled aria-label="submit edits"><Spinner size="sm"/></Button> <Button style="flex: 0" color="secondary" disabled aria-label="cancel edits">Back</Button><Button style="flex: 0; float: right;" color="danger" disabled aria-label="delete group">Delete</Button>{/if}
<Modal size="lg" isOpen={deleteOpen} toggle={toggleDeleteModal}>
    <ModalHeader toggle={toggleDeleteModal}>
        Delete group
     </ModalHeader>
         <ModalBody>
             {#if deleteErr}<Alert color="danger">{deleteErr}</Alert>{/if}
             <Label>If you're sure you want to delete this group, type out the group ID (<code>{group.id}</code>) below.</Label>
             <input class="mb-3 form-control" bind:value={deleteInput} maxlength={7} placeholder={group.id} aria-label={`type out the group id ${group.id} to confirm deletion`} use:focus>
            {#if !loading}<Button style="flex 0" color="danger" on:click={submitDelete} aria-label="confirm delete">Delete</Button> <Button style="flex: 0" color="secondary" on:click={toggleDeleteModal} aria-label="cancel delete">Back</Button>
            {:else}<Button style="flex 0" color="danger" disabled aria-label="confirm delete"><Spinner size="sm"/></Button> <Button style="flex: 0" color="secondary" disabled aria-label="cancel delete">Back</Button>
            {/if}
        </ModalBody>
</Modal>