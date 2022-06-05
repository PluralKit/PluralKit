<script lang="ts">
    import { createEventDispatcher, tick } from "svelte";
    import { ModalBody, ModalHeader, Col, Row, Input, Label, ModalFooter, Button, Spinner, Alert } from "sveltestrap";

    import { Group } from '../../api/types';
    import api from '../../api';

    export let privacyOpen: boolean;
    export let group: Group;
    const togglePrivacyModal = () => (privacyOpen = !privacyOpen);
    
    let err: string;
    let loading = false;

    function changePrivacy(e: Event) {
        const target = e.target as HTMLInputElement;
        let value = target.value;
        
        input.privacy.description_privacy = value;
        input.privacy.list_privacy = value;
        input.privacy.visibility = value;
        input.privacy.icon_privacy = value;
        input.privacy.name_privacy = value;
        input.privacy.metadata_privacy = value;
    }

    const dispatch = createEventDispatcher();

    function update() {
        dispatch('update', group);
    }

    let input: Group = {privacy: group.privacy};

    async function submit() {
        let data = input;
        err = null;

        loading = true;
        try {
            let res = await api().groups(group.id).patch({data});
            group = {...group, ...res};
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
    <select class="form-select" on:change={(e) => changePrivacy(e)} use:focus aria-label="set all to">
        <option>public</option>
        <option>private</option>
    </select>
    <hr />
    <Row>
        <Col xs={12} lg={6} class="mb-3">
            <Label>Description:</Label>
            <Input type="select" bind:value={input.privacy.description_privacy} aria-label="group description privacy">
                <option default={group.privacy.description_privacy === "public"}>public</option>
                <option default={group.privacy.description_privacy === "private"}>private</option>
            </Input>
        </Col>
        <Col xs={12} lg={6} class="mb-3">
            <Label>Name:</Label>
            <Input type="select" bind:value={input.privacy.name_privacy} aria-label="group name privacy">
                <option default={group.privacy.name_privacy === "public"}>public</option>
                <option default={group.privacy.name_privacy === "private"}>private</option>
            </Input>
        </Col>
        <Col xs={12} lg={6} class="mb-3">
            <Label>Member list:</Label>
            <Input type="select" bind:value={input.privacy.list_privacy} aria-label="group member list privacy">
                <option default={group.privacy.list_privacy === "public"}>public</option>
                <option default={group.privacy.list_privacy === "private"}>private</option>
            </Input>
        </Col>
        <Col xs={12} lg={6} class="mb-3">
            <Label>Icon:</Label>
            <Input type="select" bind:value={input.privacy.icon_privacy} aria-label="group icon privacy">
                <option default={group.privacy.icon_privacy === "public"}>public</option>
                <option default={group.privacy.icon_privacy === "private"}>private</option>
            </Input>
        </Col>
        <Col xs={12} lg={6} class="mb-3">
            <Label>Visibility:</Label>
            <Input type="select" bind:value={input.privacy.visibility} aria-label="group visibility privacy">
                <option default={group.privacy.visibility === "public"}>public</option>
                <option default={group.privacy.visibility === "private"}>private</option>
            </Input>
        </Col>
        <Col xs={12} lg={6} class="mb-3">
            <Label>Metadata:</Label>
            <Input type="select" bind:value={input.privacy.metadata_privacy} aria-label="group metadata privacy">
                <option default={group.privacy.metadata_privacy === "public"}>public</option>
                <option default={group.privacy.metadata_privacy === "private"}>private</option>
            </Input>
        </Col>
    </Row>
    {#if !loading}<Button style="flex: 0" color="primary" on:click={submit} aria-label="submit privacy edits">Submit</Button> <Button style="flex: 0" color="secondary" on:click={togglePrivacyModal} aria-label="cancel privacy edits">Back</Button>
    {:else}<Button style="flex: 0" color="primary" disabled aria-label="submit privacy edits"><Spinner size="sm"/></Button> <Button style="flex: 0" color="secondary" disabled aria-label="cancel privacy edits">Back</Button>
    {/if}