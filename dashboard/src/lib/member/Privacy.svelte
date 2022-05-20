<script lang="ts">
    import { createEventDispatcher, tick } from "svelte";
    import { Col, Row, Input, Label, Button, Alert, Spinner } from "sveltestrap";

    import { Member } from '../../api/types';
    import api from '../../api';

    let loading: boolean;
    export let privacyOpen: boolean;
    export let member: Member;
    const togglePrivacyModal = () => (privacyOpen = !privacyOpen);
    
    let err: string;

    let allPrivacy: string;

    $: { changePrivacy(allPrivacy)}

    function changePrivacy(value: string) {
        if (value) {
        input.privacy.description_privacy = value;
        input.privacy.name_privacy = value;
        input.privacy.avatar_privacy = value;
        input.privacy.birthday_privacy = value;
        input.privacy.pronoun_privacy = value;
        input.privacy.visibility = value;
        input.privacy.metadata_privacy = value;
        }
    }

    const dispatch = createEventDispatcher();

    function update() {
        dispatch('update', member);
    }

    let input: Member = {privacy: member.privacy};

    async function submit() {
        let data = input;
        err = null;

        loading = true;
        try {
            let res = await api().members(member.id).patch({data});
            member = res;
            update();
            loading = false;
            togglePrivacyModal();
        } catch (error) {
            console.log(error);
            err = error.message;
            err = err;
            loading = false;
        }
    }

    async function focus(el) {
        await tick();
        el.focus();
    }

</script>


    {#if err}
    <Alert color="danger">{err}</Alert>
    {/if}
    <Label><b>Set all to:</b></Label>
    <select class="form-select" bind:value={allPrivacy} use:focus>
        <option>public</option>
        <option>private</option>
    </select>
    <hr />
    <Row>
        <Col xs={12} lg={6} class="mb-3">
            <Label>Description:</Label>
            <Input type="select" bind:value={input.privacy.description_privacy}>
                <option default={member.privacy.description_privacy === "public"}>public</option>
                <option default={member.privacy.description_privacy === "private"}>private</option>
            </Input>
        </Col>
        <Col xs={12} lg={6} class="mb-3">
            <Label>Name:</Label>
            <Input type="select" bind:value={input.privacy.name_privacy}>
                <option default={member.privacy.name_privacy === "public"}>public</option>
                <option default={member.privacy.name_privacy === "private"}>private</option>
            </Input>
        </Col>
        <Col xs={12} lg={6} class="mb-3">
            <Label>Avatar:</Label>
            <Input type="select" bind:value={input.privacy.avatar_privacy}>
                <option default={member.privacy.avatar_privacy === "public"}>public</option>
                <option default={member.privacy.avatar_privacy === "private"}>private</option>
            </Input>
        </Col>
        <Col xs={12} lg={6} class="mb-3">
            <Label>Birthday:</Label>
            <Input type="select" bind:value={input.privacy.birthday_privacy}>
                <option default={member.privacy.birthday_privacy === "public"}>public</option>
                <option default={member.privacy.birthday_privacy === "private"}>private</option>
            </Input>
        </Col>
        <Col xs={12} lg={6} class="mb-3">
            <Label>Pronouns:</Label>
            <Input type="select" bind:value={input.privacy.pronoun_privacy}>
                <option default={member.privacy.pronoun_privacy === "public"}>public</option>
                <option default={member.privacy.pronoun_privacy === "private"}>private</option>
            </Input>
        </Col>
        <Col xs={12} lg={6} class="mb-3">
            <Label>Visibility:</Label>
            <Input type="select" bind:value={input.privacy.visibility}>
                <option default={member.privacy.visibility === "public"}>public</option>
                <option default={member.privacy.visibility === "private"}>private</option>
            </Input>
        </Col>
        <Col xs={12} lg={6} class="mb-3">
            <Label>Metadata:</Label>
            <Input type="select" bind:value={input.privacy.metadata_privacy}>
                <option default={member.privacy.metadata_privacy === "public"}>public</option>
                <option default={member.privacy.metadata_privacy === "private"}>private</option>
            </Input>
        </Col>
    </Row>
{#if !loading}<Button style="flex: 0" color="primary" on:click={submit}>Submit</Button> <Button style="flex: 0" color="secondary" on:click={togglePrivacyModal}>Back</Button>
{:else}<Button style="flex: 0" color="primary" disabled><Spinner size="sm"/></Button> <Button style="flex: 0" color="secondary" disabled>Back</Button>
{/if}