<script lang="ts">
    import { Row, Col, Input, Button, Label, Alert, Spinner, Accordion, AccordionItem, CardTitle } from 'sveltestrap';
    import { Group } from '../../api/types';
    import api from '../../api';
    import autosize from 'svelte-autosize';
    import { createEventDispatcher } from 'svelte';
    import FaPlus from 'svelte-icons/fa/FaPlus.svelte';

    const descriptions: string[] = JSON.parse(localStorage.getItem("pk-config"))?.description_templates;

    let loading: boolean = false;
    let err: string[] = [];
    let message: string;
    let privacyMode = false;

    let defaultGroup: Group = {
        privacy: {
            description_privacy: "public",
            metadata_privacy: "public",
            list_privacy: "public",
            icon_privacy: "public",
            name_privacy: "public",
            visibility: "public"
        }
    }

    const dispatch = createEventDispatcher();

    function create(data: Group) {
        dispatch('create', data);
    }

    let input: Group = JSON.parse(JSON.stringify(defaultGroup));

    async function submit() {
        let data = input;
        message = "";
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
        try {
            let res: Group = await api().groups().post({data});
            res.members = [];
            create(res);
            input = JSON.parse(JSON.stringify(defaultGroup));
            message = `Group ${data.name} successfully created!`
            err = [];
            loading = false;
        } catch (error) {
            console.log(error);
            err.push(error.message);
            err = err;
            loading = false;
        }
    }

</script>

<Accordion class="mb-3">
    <AccordionItem>
        <CardTitle slot="header" style="margin-top: 0px; margin-bottom: 0px; outline: none; align-items: center;" class="d-flex align-middle w-100 p-2">
            <div class="icon d-inline-block">
                <FaPlus/>
            </div>
            <span style="vertical-align: middle;">Add new Group</span>
        </CardTitle>
        {#if message}
            <Alert color="success">{@html message}</Alert>
        {/if}
        {#each err as error}
            <Alert color="danger">{@html error}</Alert>
        {/each}
        <Row>
            <Col xs={12} lg={4} class="mb-2">
                <Label>Name:</Label>
                <Input bind:value={input.name} maxlength={100} type="text" />
            </Col>
            <Col xs={12} lg={4} class="mb-2">
                <Label>Display name:</Label>
                <textarea class="form-control" style="resize: none; height: 1em" bind:value={input.display_name} maxlength={100} type="text"/>
            </Col>
            <Col xs={12} lg={4} class="mb-2">
                <Label>Color:</Label>
                <Input bind:value={input.color} type="text"/>
            </Col>
            <Col xs={12} lg={4} class="mb-2">
                <Label>Icon url:</Label>
                <Input bind:value={input.icon} maxlength={256} type="url"/>
            </Col>
            <Col xs={12} lg={4} class="mb-2">
                <Label>Banner url:</Label>
                <Input bind:value={input.banner} maxlength={256} type="url"/>
            </Col>
            <Col xs={12} lg={4} class="mb-2">
                <Label>Privacy:</Label>
                <Button class="w-100" color="secondary" on:click={() => privacyMode = !privacyMode}>Toggle privacy</Button>
            </Col>
        </Row>
        {#if privacyMode}
        <hr />
        <Row>
            <Col xs={12} lg={4} class="mb-3">
                <Label>Description:</Label>
                <Input type="select" bind:value={input.privacy.description_privacy}>
                    <option>public</option>
                    <option>private</option>
                </Input>
            </Col>
            <Col xs={12} lg={4} class="mb-3">
                <Label>Name:</Label>
                <Input type="select" bind:value={input.privacy.name_privacy}>
                    <option>public</option>
                    <option>private</option>
                </Input>
            </Col>
            <Col xs={12} lg={4} class="mb-3">
                <Label>Member list:</Label>
                <Input type="select" bind:value={input.privacy.list_privacy}>
                    <option>public</option>
                    <option>private</option>
                </Input>
            </Col>
            <Col xs={12} lg={4} class="mb-3">
                <Label>Icon:</Label>
                <Input type="select" bind:value={input.privacy.icon_privacy}>
                    <option>public</option>
                    <option>private</option>
                </Input>
            </Col>
            <Col xs={12} lg={4} class="mb-3">
                <Label>Visibility:</Label>
                <Input type="select" bind:value={input.privacy.visibility}>
                    <option>public</option>
                    <option>private</option>
                </Input>
            </Col>
            <Col xs={12} lg={4} class="mb-3">
                <Label>Metadata:</Label>
                <Input type="select" bind:value={input.privacy.metadata_privacy}>
                    <option>public</option>
                    <option>private</option>
                </Input>
            </Col>
        </Row>
        <hr />
        {/if}
        <div class="my-2">
            <b>Description:</b><br />
            {#if descriptions.length > 0 && descriptions[0].trim() != ""}
            <Button size="sm" color="primary" on:click={() => input.description = descriptions[0]}>Template 1</Button>
            {/if}
            {#if descriptions.length > 1 && descriptions[1].trim() != ""}
            <Button size="sm" color="primary" on:click={() => input.description = descriptions[1]}>Template 2</Button>
            {/if}
            {#if descriptions.length > 2 && descriptions[2].trim() != ""}
            <Button size="sm" color="primary" on:click={() => input.description = descriptions[2]}>Template 3</Button>
            {/if}
            <br>
            <textarea class="form-control" bind:value={input.description} maxlength={1000} use:autosize />
        </div>
        {#if !loading && input.name}<Button style="flex: 0" color="primary" on:click={submit}>Submit</Button>
        {:else if !input.name }<Button style="flex: 0" color="primary" disabled>Submit</Button>
        {:else}<Button style="flex: 0" color="primary" disabled><Spinner size="sm"/></Button>{/if}
    </AccordionItem>
</Accordion>