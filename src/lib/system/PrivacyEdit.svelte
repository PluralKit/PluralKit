<script lang="ts">
    import Sys from '../../api/system';
    import { Input, Row, Col, Button, Label } from 'sveltestrap';
    import { currentUser } from '../../stores';
    import PKAPI from '../../api';

    export let loading = false;
    export let user: Sys;
    export let editMode: boolean;

    let err: string;

    let input = new Sys(user);

    async function submit() {
        let data = input;
        err = null;

        loading = true;
        const api = new PKAPI();
        try {
            let res = await api.patchSystem({token: localStorage.getItem("pk-token"), data: data});
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

<Label><b>Set all to:</b></Label>
<Input type="select" bind:value={allPrivacy}>
    <option>public</option>
    <option>private</option>
</Input>
<hr />
<Row>
    <Col xs={12} lg={4} class="mb-3">
        <Label>Description:</Label>
        <Input type="select" bind:value={input.privacy.description_privacy}>
            <option default={user.privacy.description_privacy === "public"}>public</option>
            <option default={user.privacy.description_privacy === "private"}>private</option>
        </Input>
    </Col>
    <Col xs={12} lg={4} class="mb-3">
        <Label>Member list:</Label>
        <Input type="select" bind:value={input.privacy.member_list_privacy}>
            <option default={user.privacy.member_list_privacy === "public"}>public</option>
            <option default={user.privacy.member_list_privacy === "private"}>private</option>
        </Input>
    </Col>
    <Col xs={12} lg={4} class="mb-3">
        <Label>Group list:</Label>
        <Input type="select" bind:value={input.privacy.group_list_privacy}>
            <option default={user.privacy.group_list_privacy === "public"}>public</option>
            <option default={user.privacy.group_list_privacy === "private"}>private</option>
        </Input>
    </Col>
    <Col xs={12} lg={4} class="mb-3">
        <Label>Current front:</Label>
        <Input type="select" bind:value={input.privacy.front_privacy}>
            <option default={user.privacy.front_privacy === "public"}>public</option>
            <option default={user.privacy.front_privacy === "private"}>private</option>
        </Input>
    </Col>
    <Col xs={12} lg={4} class="mb-3">
        <Label>Front history:</Label>
        <Input type="select" bind:value={input.privacy.front_history_privacy}>
            <option default={user.privacy.front_history_privacy === "public"}>public</option>
            <option default={user.privacy.front_history_privacy === "private"}>private</option>
        </Input>
    </Col>
</Row>
<Button style="flex: 0" color="primary" on:click={submit}>Submit</Button> <Button style="flex: 0" color="light" on:click={() => editMode = false}>Back</Button>