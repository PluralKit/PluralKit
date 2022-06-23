<script lang="ts">
    import { Row, Col, Input, Button, Label, Alert, Spinner, Modal, ModalHeader, ModalBody } from 'sveltestrap';
    import { createEventDispatcher, tick } from 'svelte';
    import autosize from 'svelte-autosize';
    import moment from 'moment';

    import { Member } from '../../api/types'
    import api from '../../api';

    const descriptions: string[] = JSON.parse(localStorage.getItem("pk-config"))?.description_templates;

    let loading: boolean = false;
    export let member: Member;
    export let editMode: boolean;

    let err: string[] = [];
    let success = false;

    let input: Member = member;

    const dispatch = createEventDispatcher();
    
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
            let allowedFormats = ['YYYY-MM-DD','YYYY-M-D', 'YYYY-MM-D', 'YYYY-M-DD'];

            // replace all brackets with dashes
            if (data.birthday.includes('/')) {
                data.birthday = data.birthday.replaceAll('/', '-');
            }

            // add a generic year if there's no year included
            // NOTE: for some reason moment parses a date with only a month and day as a YEAR and a month
            // so I'm just checking by the amount of dashes in the string
            if (data.birthday.split('-').length - 1 === 1) {
                data.birthday = '0004-' + data.birthday;
            }

            // try matching the birthday to the YYYY-MM-DD format
            if (moment(data.birthday, allowedFormats, true).isValid()) {
                // convert the format to have months and days as double digits.
                data.birthday = moment(data.birthday, 'YYYY-MM-DD').format('YYYY-MM-DD');
            } else {
                err.push(`${data.birthday} is not a valid date, please use the following format: YYYY-MM-DD. (example: 2019-07-21)`);
            }
        }

        err = err;
        if (err.length > 0) return;

        loading = true;
        try {
            let res = await api().members(member.id).patch({data});
            member = res;
            success = true;
        } catch (error) {
            console.log(error);
            err.push(error.message);
            err = err;
        }
        loading = false;
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
        try {
            await api().members(member.id).delete();
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

    async function focus(el) {
        await tick();
        el.focus();
    }
</script>

{#each err as error}
    <Alert fade={false} color="danger">{@html error}</Alert>
{/each}
{#if success}
<Alert fade={false} color="success">Member information updated!</Alert>
{/if}
<Row>
    <Col xs={12} lg={4} class="mb-2">
        <Label>Name:</Label>
        <Input bind:value={input.name} maxlength={100} type="text" placeholder={member.name} aria-label="member name"/>
    </Col>
    <Col xs={12} lg={4} class="mb-2">
        <Label>Display name:</Label>
        <textarea class="form-control" style="resize: none; height: 1em" bind:value={input.display_name} maxlength={100} type="text" placeholder={member.display_name} aria-label="member display name" />
    </Col>
    <Col xs={12} lg={4} class="mb-2">
        <Label>Pronouns:</Label>
        <textarea class="form-control" style="resize: none; height: 1em" bind:value={input.pronouns} maxlength={100} type="text" placeholder={member.pronouns} aria-label="member pronouns" />
    </Col>
    <Col xs={12} lg={4} class="mb-2">
        <Label>Birthday:</Label>
        <Input bind:value={input.birthday} maxlength={100} type="text" placeholder={member.birthday} aria-label="member birthday" />
    </Col>
    <Col xs={12} lg={4} class="mb-2">
        <Label>Color:</Label>
        <Input bind:value={input.color} type="text" placeholder={member.color} aria-label="member color" />
    </Col>
    <Col xs={12} lg={4} class="mb-2">
        <Label>Avatar url:</Label>
        <Input bind:value={input.avatar_url} maxlength={256} type="url" placeholder={member.avatar_url} aria-label="member avatar url"/>
    </Col>
    <Col xs={12} lg={4} class="mb-2">
        <Label>Banner url:</Label>
        <Input bind:value={input.banner} maxlength={256} type="url" placeholder={member.banner} aria-label="member banner url"/>
    </Col>
</Row>
<div class="my-2">
    <b>Description:</b><br />
    {#if descriptions.length > 0 && descriptions[0].trim() != ""}
    <Button size="sm" color="primary" on:click={() => input.description = descriptions[0]} aria-label="use template 1">Template 1</Button>
    {/if}
    {#if descriptions.length > 1 && descriptions[1].trim() != ""}
    <Button size="sm" color="primary" on:click={() => input.description = descriptions[1]} aria-label="use template 2">Template 2</Button>
    {/if}
    {#if descriptions.length > 2 && descriptions[2].trim() != ""}
    <Button size="sm" color="primary" on:click={() => input.description = descriptions[2]} aria-label="use template 3">Template 3</Button>
    {/if}
    <br>
    <textarea class="form-control" bind:value={input.description} maxlength={1000} use:autosize placeholder={member.description} aria-label="member description"/>
</div>
{#if !loading}<Button style="flex: 0" color="primary" on:click={submit} aria-label="submit edits" >Submit</Button> <Button style="flex: 0" color="secondary" on:click={() => editMode = false} aria-label="cancel edits">Back</Button><Button style="flex: 0; float: right;" color="danger" on:click={toggleDeleteModal} aria-label="delete member">Delete</Button>
{:else}<Button style="flex: 0" color="primary" disabled  aria-label="submit edits"><Spinner size="sm"/></Button> <Button style="flex: 0" color="secondary" disabled aria-label="cancel edits">Back</Button><Button style="flex: 0; float: right;" color="danger" disabled aria-label="delete member">Delete</Button>{/if}
<Modal size="lg" isOpen={deleteOpen} toggle={toggleDeleteModal}>
    <ModalHeader toggle={toggleDeleteModal}>
        Delete member
     </ModalHeader>
         <ModalBody>
             {#if deleteErr}<Alert color="danger">{deleteErr}</Alert>{/if}
             <Label>If you're sure you want to delete this member, type out the member ID (<code>{member.id}</code>) below.</Label>
             <input class="mb-3 form-control" bind:value={deleteInput} maxlength={7} placeholder={member.id} aria-label={`type out the member id ${member.id} to confirm deletion`} use:focus>
            {#if !loading}<Button style="flex 0" color="danger" on:click={submitDelete} aria-label="confirm delete">Delete</Button> <Button style="flex: 0" color="secondary" on:click={toggleDeleteModal} aria-label="cancel deletion">Back</Button>
            {:else}<Button style="flex 0" color="danger" disabled><Spinner size="sm"/></Button> <Button style="flex: 0" color="secondary" disabled aria-label="cancel deletion">Back</Button>
            {/if}
        </ModalBody>
</Modal>