<script lang="ts">
    import { Row, Col, Input, Button, Label, Alert } from 'sveltestrap';
    import Sys from '../../api/system';
    import PKAPI from '../../api';
    import autosize from 'svelte-autosize';
    // import moment from 'moment-timezone';
    import { currentUser } from '../../stores';

    export let editMode: boolean;
    export let user: Sys;
    export let loading: boolean;

    let err: string[] = [];

    let input = new Sys({name: user.name, tag: user.tag, color: user.color, avatar_url: user.avatar_url, banner: user.banner, description: user.description});
    
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

        /* if (data.timezone && !moment.tz.zone(data.timezone)) {
            err.push(`"${data.timezone}" is not a valid timezone, check out <a target="_blank" style="color: var(--bs-body-color);" href="https://xske.github.io/tz/">this site</a> to see your current timezone!`);
        } */

        err = err;
        if (err.length > 0) return;

        loading = true;
        const api = new PKAPI();
        try {
            let res = await api.patchSystem({token: localStorage.getItem("pk-token"), data: data});
            user = res;
            currentUser.update(() => res);
            err = [];
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
        <Input bind:value={input.name} maxlength={100} type="text" placeholder={user.name} />
    </Col>
    <Col xs={12} lg={4} class="mb-2">
        <Label>Tag:</Label>
        <Input bind:value={input.tag} maxlength={100} type="text" placeholder={user.tag} />
    </Col>
    <!-- <Col xs={12} lg={4} class="mb-2">
        <Label>Timezone:</Label>
        <Input bind:value={input.timezone} type="text" placeholder={user.timezone} />
    </Col> -->
    <Col xs={12} lg={4} class="mb-2">
        <Label>Color:</Label>
        <Input bind:value={input.color} type="text" placeholder={user.color}/>
    </Col>
    <Col xs={12} lg={4} class="mb-2">
        <Label>Avatar url:</Label>
        <Input bind:value={input.avatar_url} maxlength={256} type="url" placeholder={user.avatar_url}/>
    </Col>
    <Col xs={12} lg={4} class="mb-2">
        <Label>Banner url:</Label>
        <Input bind:value={input.banner} maxlength={256} type="url" placeholder={user.banner}/>
    </Col>
</Row>
<div class="my-2">
    <b>Description:</b><br />
    <textarea class="form-control" bind:value={input.description} maxlength={1000} use:autosize placeholder={user.description}/>
</div>
<Button style="flex: 0" color="primary" on:click={submit}>Submit</Button> <Button style="flex: 0" color="light" on:click={() => editMode = false}>Back</Button>