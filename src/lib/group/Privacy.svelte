<script lang="ts">
    import Group from "../../api/group";
    import PKAPI from "../../api";
    import { createEventDispatcher } from "svelte";
    import { ModalBody, ModalHeader, Col, Row, Input, Label, ModalFooter, Button, Spinner, Alert } from "sveltestrap";

    export let privacyOpen: boolean;
    export let group: Group;
    const togglePrivacyModal = () => (privacyOpen = !privacyOpen);
    
    let err: string;
    export let loading = false;

    let allPrivacy: string;

    $: { changePrivacy(allPrivacy)}

    function changePrivacy(value: string) {
        if (value) {
        input.privacy.description_privacy = value;
        input.privacy.list_privacy = value;
        input.privacy.visibility = value;
        input.privacy.icon_privacy = value;
        }
    }

    const dispatch = createEventDispatcher();

    function update() {
        dispatch('update', group);
    }

    let input = new Group(group);

    async function submit() {
        let data = input;
        err = null;

        loading = true;
        const api = new PKAPI();
        try {
            let res = await api.patchGroup({token: localStorage.getItem("pk-token"), id: group.id, data: data});
            group = res;
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
</script>


    {#if err}
    <Alert color="danger">{err}</Alert>
    {/if}
    <Label><b>Set all to:</b></Label>
    <Input type="select" bind:value={allPrivacy}>
        <option>public</option>
        <option>private</option>
    </Input>
    <hr />
    <Row>
        <Col xs={12} lg={6} class="mb-3">
            <Label>Description:</Label>
            <Input type="select" bind:value={input.privacy.description_privacy}>
                <option default={group.privacy.description_privacy === "public"}>public</option>
                <option default={group.privacy.description_privacy === "private"}>private</option>
            </Input>
        </Col>
        <Col xs={12} lg={6} class="mb-3">
            <Label>Member list:</Label>
            <Input type="select" bind:value={input.privacy.list_privacy}>
                <option default={group.privacy.list_privacy === "public"}>public</option>
                <option default={group.privacy.list_privacy === "private"}>private</option>
            </Input>
        </Col>
        <Col xs={12} lg={6} class="mb-3">
            <Label>Visibility:</Label>
            <Input type="select" bind:value={input.privacy.visibility}>
                <option default={group.privacy.visibility === "public"}>public</option>
                <option default={group.privacy.visibility === "private"}>private</option>
            </Input>
        </Col>
        <Col xs={12} lg={6} class="mb-3">
            <Label>Icon :</Label>
            <Input type="select" bind:value={input.privacy.icon_privacy}>
                <option default={group.privacy.icon_privacy === "public"}>public</option>
                <option default={group.privacy.icon_privacy === "private"}>private</option>
            </Input>
        </Col>
    </Row>
    <Button style="flex: 0" color="primary" on:click={submit}>Submit</Button> <Button style="flex: 0" color="secondary" on:click={togglePrivacyModal}>Back</Button>