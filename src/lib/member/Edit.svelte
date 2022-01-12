<script lang="ts">
    import { Row, Col, Input, Button, Label, Alert, Spinner, Modal, ModalHeader, ModalBody } from 'sveltestrap';
    import { createEventDispatcher } from 'svelte';
    import autosize from 'svelte-autosize';
    import moment from 'moment';

    import Member from '../../api/member';
    import PKAPI from '../../api';

    let loading: boolean = false;
    export let member: Member;
    export let editMode: boolean;

    let err: string[] = [];

    let input = new Member({name: member.name, display_name: member.display_name, birthday: member.birthday, pronouns: member.pronouns, color: member.color, description: member.description, banner: member.banner, avatar_url: member.avatar_url});

    const dispatch = createEventDispatcher();

    function update() {
        dispatch('update', member);
    }

    function deletion() {
        dispatch('deletion', member.id);
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

        if (data.birthday) {
            if (!moment(data.birthday, 'YYYY-MM-DD').isValid()) {
                if (moment(data.birthday, 'MM-DD').isValid()) {
                    data.birthday = '0004-' + data.birthday;
                } else {
                    err.push(`${data.birthday} is not a valid date, please use the following format: YYYY-MM-DD. (example: 2019-07-21)`);
                }
            }
            if (data.birthday.includes('/')) {
                data.birthday.replace('/', '-');
            }
        }

        err = err;
        if (err.length > 0) return;

        loading = true;
        const api = new PKAPI();
        try {
            let res = await api.patchMember({token: localStorage.getItem("pk-token"), id: member.id, data: data});
            member = res;
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
            deleteErr = "Please type out the member ID.";
            return;
        }

        if (deleteInput.trim().toLowerCase() !== member.id) {
            deleteErr = "This member's ID does not match the provided ID.";
            return;
        }
        loading = true;
        const api = new PKAPI();
        try {
            await api.deleteMember({token: localStorage.getItem("pk-token"), id: member.id});
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
        <Input bind:value={input.name} maxlength={100} type="text" placeholder={member.name} />
    </Col>
    <Col xs={12} lg={4} class="mb-2">
        <Label>Display name:</Label>
        <textarea class="form-control" style="resize: none; height: 1em" bind:value={input.display_name} maxlength={100} type="text" placeholder={member.display_name} />
    </Col>
    <Col xs={12} lg={4} class="mb-2">
        <Label>Pronouns:</Label>
        <textarea class="form-control" style="resize: none; height: 1em" bind:value={input.pronouns} maxlength={100} type="text" placeholder={member.pronouns} />
    </Col>
    <Col xs={12} lg={4} class="mb-2">
        <Label>Birthday:</Label>
        <Input bind:value={input.birthday} maxlength={100} type="text" placeholder={member.birthday} />
    </Col>
    <Col xs={12} lg={4} class="mb-2">
        <Label>Color:</Label>
        <Input bind:value={input.color} type="text" placeholder={member.color}/>
    </Col>
    <Col xs={12} lg={4} class="mb-2">
        <Label>Avatar url:</Label>
        <Input bind:value={input.avatar_url} maxlength={256} type="url" placeholder={member.avatar_url}/>
    </Col>
    <Col xs={12} lg={4} class="mb-2">
        <Label>Banner url:</Label>
        <Input bind:value={input.banner} maxlength={256} type="url" placeholder={member.banner}/>
    </Col>
</Row>
<div class="my-2">
    <b>Description:</b><br />
    <textarea class="form-control" bind:value={input.description} maxlength={1000} use:autosize placeholder={member.description}/>
</div>
{#if !loading}<Button style="flex: 0" color="primary" on:click={submit}>Submit</Button> <Button style="flex: 0" color="secondary" on:click={() => editMode = false}>Back</Button><Button style="flex: 0; float: right;" color="danger" on:click={toggleDeleteModal}>Delete</Button>
{:else}<Button style="flex: 0" color="primary" disabled><Spinner size="sm"/></Button> <Button style="flex: 0" color="secondary" disabled>Back</Button><Button style="flex: 0; float: right;" color="danger" disabled>Delete</Button>{/if}
<Modal size="lg" isOpen={deleteOpen} toggle={toggleDeleteModal}>
    <ModalHeader toggle={toggleDeleteModal}>
        Delete member
     </ModalHeader>
         <ModalBody>
             {#if deleteErr}<Alert color="danger">{deleteErr}</Alert>{/if}
             <Label>If you're sure you want to delete this member, type out the member ID (<code>{member.id}</code>) below.</Label>
             <Input class="mb-3" bind:value={deleteInput} maxlength={7} placeholder={member.id}></Input>
            {#if !loading}<Button style="flex 0" color="danger" on:click={submitDelete}>Delete</Button> <Button style="flex: 0" color="secondary" on:click={toggleDeleteModal}>Back</Button>
            {:else}<Button style="flex 0" color="danger" disabled><Spinner size="sm"/></Button> <Button style="flex: 0" color="secondary" disabled>Back</Button>
            {/if}
        </ModalBody>
</Modal>