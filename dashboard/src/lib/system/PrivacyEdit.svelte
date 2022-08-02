<script lang="ts">
    import { Input, Row, Col, Button, Label, Alert } from 'sveltestrap';
    import { currentUser } from '../../stores';

    import { System } from '../../api/types';
    import api from '../../api';

    export let loading = false;
    export let user: System;
    export let editMode: boolean;

    let err: string;

    let input: System = {privacy: user.privacy};

    async function submit() {
        let data = input;
        err = null;

        loading = true;
        try {
            let res = await api().systems("@me").patch({data});
            user = res;
            currentUser.update(() => res);
            editMode = false;
            loading = false;
        } catch (error) {
            console.log(error);
            err = error.message;
            err = err;
            loading = false;
        }
    }

    let allPrivacy: string;

    $: { changePrivacy(allPrivacy)}

    function changePrivacy(value: string) {
        if (value) {
        input.privacy.description_privacy = value;
        input.privacy.member_list_privacy = value;
        input.privacy.group_list_privacy = value;
        input.privacy.front_privacy = value;
        input.privacy.front_history_privacy = value;
        }
    }
</script>

{#if err}
    <Alert color="danger">{err}</Alert>
{/if}
<Label><b>Set all to:</b></Label>
<Input type="select" bind:value={allPrivacy} aria-label="set all to">
    <option>public</option>
    <option>private</option>
</Input>
<hr />
<Row>
    <Col xs={12} lg={4} class="mb-3">
        <Label>Description:</Label>
        <Input type="select" bind:value={input.privacy.description_privacy} aria-label="system description privacy">
            <option default={user.privacy.description_privacy === "public"}>public</option>
            <option default={user.privacy.description_privacy === "private"}>private</option>
        </Input>
    </Col>
    <Col xs={12} lg={4} class="mb-3">
        <Label>Member list:</Label>
        <Input type="select" bind:value={input.privacy.member_list_privacy} aria-label="system member list privacy">
            <option default={user.privacy.member_list_privacy === "public"}>public</option>
            <option default={user.privacy.member_list_privacy === "private"}>private</option>
        </Input>
    </Col>
    <Col xs={12} lg={4} class="mb-3">
        <Label>Group list:</Label>
        <Input type="select" bind:value={input.privacy.group_list_privacy} aria-label="system group list privacy">
            <option default={user.privacy.group_list_privacy === "public"}>public</option>
            <option default={user.privacy.group_list_privacy === "private"}>private</option>
        </Input>
    </Col>
    <Col xs={12} lg={4} class="mb-3">
        <Label>Current front:</Label>
        <Input type="select" bind:value={input.privacy.front_privacy} aria-label="system front privacy">
            <option default={user.privacy.front_privacy === "public"}>public</option>
            <option default={user.privacy.front_privacy === "private"}>private</option>
        </Input>
    </Col>
    <Col xs={12} lg={4} class="mb-3">
        <Label>Front history:</Label>
        <Input type="select" bind:value={input.privacy.front_history_privacy} aria-label="system front history privacy">
            <option default={user.privacy.front_history_privacy === "public"}>public</option>
            <option default={user.privacy.front_history_privacy === "private"}>private</option>
        </Input>
    </Col>
</Row>
<Button style="flex: 0" color="primary" on:click={submit} aria-label="submit privacy edit">Submit</Button> <Button style="flex: 0" color="secondary" on:click={() => editMode = false} aria-label="cancel privacy edit">Back</Button>