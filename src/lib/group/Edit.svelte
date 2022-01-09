<script lang="ts">
    import { Row, Col, Input, Button, Label, Alert, Spinner, Modal, ModalHeader, ModalBody } from 'sveltestrap';
    import { createEventDispatcher } from 'svelte';
    import Group from '../../api/group';
    import PKAPI from '../../api';
    import autosize from 'svelte-autosize';

    let loading: boolean = false;
    export let group: Group;
    export let editMode: boolean;

    let err: string[] = [];

    let input = new Group(group);

    const dispatch = createEventDispatcher();

    function update() {
        dispatch('update', group);
    }

    function deletion() {
        dispatch('deletion', group.id);
    }

    async function submit() {
        let data = input;
        err = [];

        if (data.color && !/^#?[A-Fa-f0-9]{6}$/.test(input.color)) {
            err.push(`"${data.color}" is not a valid color, the color must be a 6-digit hex code. (example: #ff0000)`);
        } else if (data.color) {
            if (data.color.startsWith("#")) {
                data.color = input.color.slice(1, input.color.length);
            }
        }

        err = err;
        if (err.length > 0) return;

        loading = true;
        const api = new PKAPI();
        try {
            let res = await api.patchGroup({token: localStorage.getItem("pk-token"), id: group.id, data: data});
            group = res;
            err = [];
            update();
            editMode = false;
            loading = false;
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
        const api = new PKAPI();
        try {
            await api.deleteGroup({token: localStorage.getItem("pk-token"), id: group.id});
            deleteErr = null;
            toggleDeleteModal();
            loading = false;
            deletion();
        } catch (error) {
            console.log(error);
            deleteErr = error.message;
            loading = false;
        }
    }
</script>

{#each err as error}
    <Alert color="danger">{@html error}</Alert>
{/each}
<Row>
    <Col xs={12} lg={4} class="mb-2">
        <Label>Name:</Label>
        <Input bind:value={input.name} maxlength={100} type="text" placeholder={group.name} />
    </Col>
    <Col xs={12} lg={4} class="mb-2">
        <Label>Display name:</Label>
        <textarea class="form-control" style="resize: none; height: 1em" bind:value={input.display_name} maxlength={100} type="text" placeholder={group.display_name} />
    </Col>
    <Col xs={12} lg={4} class="mb-2">
        <Label>Color:</Label>
        <Input bind:value={input.color} type="text" placeholder={group.color}/>
    </Col>
    <Col xs={12} lg={4} class="mb-2">
        <Label>Avatar url:</Label>
        <Input bind:value={input.icon} maxlength={256} type="url" placeholder={group.icon}/>
    </Col>
    <Col xs={12} lg={4} class="mb-2">
        <Label>Banner url:</Label>
        <Input bind:value={input.banner} maxlength={256} type="url" placeholder={group.banner}/>
    </Col>
</Row>
<div class="my-2">
    <b>Description:</b><br />
    <textarea class="form-control" bind:value={input.description} maxlength={1000} use:autosize placeholder={group.description}/>
</div>
{#if !loading}<Button style="flex: 0" color="primary" on:click={submit}>Submit</Button> <Button style="flex: 0" color="secondary" on:click={() => editMode = false}>Back</Button><Button style="flex: 0; float: right;" color="danger" on:click={toggleDeleteModal}>Delete</Button>
{:else}<Button style="flex: 0" color="primary" disabled><Spinner size="sm"/></Button> <Button style="flex: 0" color="secondary" disabled>Back</Button><Button style="flex: 0; float: right;" color="danger" disabled>Delete</Button>{/if}
<Modal size="lg" isOpen={deleteOpen} toggle={toggleDeleteModal}>
    <ModalHeader toggle={toggleDeleteModal}>
        Delete member
     </ModalHeader>
         <ModalBody>
             {#if deleteErr}<Alert color="danger">{deleteErr}</Alert>{/if}
             <Label>If you're sure you want to delete this member, type out the member ID (<code>{group.id}</code>) below.</Label>
             <Input class="mb-3" bind:value={deleteInput} maxlength={7} placeholder={group.id}></Input>
            {#if !loading}<Button style="flex 0" color="danger" on:click={submitDelete}>Delete</Button> <Button style="flex: 0" color="secondary" on:click={toggleDeleteModal}>Back</Button>
            {:else}<Button style="flex 0" color="danger" disabled><Spinner size="sm"/></Button> <Button style="flex: 0" color="secondary" disabled>Back</Button>
            {/if}
        </ModalBody>
</Modal>