<script lang="ts">
    import { Row, Col, Input, Button, Label, Alert } from 'sveltestrap';
    import { createEventDispatcher } from 'svelte';
    import Group from '../../api/group';
    import PKAPI from '../../api';
    import autosize from 'svelte-autosize';

    export let loading: boolean = false;
    export let group: Group;
    export let editMode: boolean;

    let err: string[] = [];

    let input = new Group(group);

    const dispatch = createEventDispatcher();

    function update() {
        dispatch('update', group);
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
        <Input bind:value={input.display_name} maxlength={100} type="text" placeholder={group.display_name} />
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
<Button style="flex: 0" color="primary" on:click={submit}>Submit</Button> <Button style="flex: 0" color="light" on:click={() => editMode = false}>Back</Button>