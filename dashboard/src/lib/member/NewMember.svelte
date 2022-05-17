<script lang="ts">
    import { Accordion, AccordionItem, Row, Col, Input, Button, Label, Alert, Spinner, CardTitle, InputGroup } from 'sveltestrap';
    import { createEventDispatcher } from 'svelte';
    import autosize from 'svelte-autosize';
    import moment from 'moment';
    import FaPlus from 'svelte-icons/fa/FaPlus.svelte';
    import { Member } from '../../api/types';
    import api from '../../api';

    const descriptions: string[] = JSON.parse(localStorage.getItem("pk-config"))?.description_templates;

    let err: string[] = [];
    let message: string;
    let loading: boolean = false;
    let privacyMode: boolean = false;
    let proxyTagMode: boolean = false;

    let defaultMember = {
        privacy: {
            visibility: "public",
            metadata_privacy: "public",
            description_privacy: "public",
            pronoun_privacy: "public",
            birthday_privacy: "public",
            name_privacy: "public",
            avatar_privacy: "public"
        },
        proxy_tags: [
            {
                prefix: "",
                suffix: ""
            }
        ]
    };

    let input: Member = defaultMember;

    const dispatch = createEventDispatcher();

    function create() {
        dispatch('create', input);
    }

    async function submit() {
        let data = input;
        message = "";
        err = [];

        if (input.proxy_tags.some(tag => tag.prefix && tag.suffix && tag.prefix.length + tag.suffix.length + 4 > 100)) {
            err.push("One of your proxy tags is too long (prefix + 'text' + suffix must be shorter than 100 characters). Please shorten this tag, or remove it.");
        }

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
        try {
            let res = await api().members().post({data});
            input = res;
            err = [];
            create();
            input = defaultMember;
            message = `Member ${data.name} successfully created!`
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
            <span style="vertical-align: middle;">Add new Member</span>
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
                <Input bind:value={input.name} maxlength={100} type="text" placeholder={input.name} />
            </Col>
            <Col xs={12} lg={4} class="mb-2">
                <Label>Display name:</Label>
                <textarea class="form-control" style="resize: none; height: 1em" bind:value={input.display_name} maxlength={100} type="text" placeholder={input.display_name} />
            </Col>
            <Col xs={12} lg={4} class="mb-2">
                <Label>Pronouns:</Label>
                <textarea class="form-control" style="resize: none; height: 1em" bind:value={input.pronouns} maxlength={100} type="text" placeholder={input.pronouns} />
            </Col>
            <Col xs={12} lg={4} class="mb-2">
                <Label>Birthday:</Label>
                <Input bind:value={input.birthday} maxlength={100} type="text" placeholder={input.birthday} />
            </Col>
            <Col xs={12} lg={4} class="mb-2">
                <Label>Color:</Label>
                <Input bind:value={input.color} type="text" placeholder={input.color}/>
            </Col>
            <Col xs={12} lg={4} class="mb-2">
                <Label>Avatar url:</Label>
                <Input bind:value={input.avatar_url} maxlength={256} type="url" placeholder={input.avatar_url}/>
            </Col>
            <Col xs={12} lg={4} class="mb-2">
                <Label>Banner url:</Label>
                <Input bind:value={input.banner} maxlength={256} type="url" placeholder={input.banner}/>
            </Col>
            <Col xs={12} lg={4} class="mb-2">
                <Label>Privacy:</Label>
                <Button class="w-100" color="secondary" on:click={() => privacyMode = !privacyMode}>Toggle privacy</Button>
            </Col>
            <Col xs={12} lg={4} class="mb-2">
                <Label>Proxy tags:</Label>
                <Button class="w-100" color="secondary" on:click={() => proxyTagMode = !proxyTagMode}>Toggle proxy tags</Button>
            </Col>
        </Row>
        {#if privacyMode}
        <hr/>
        <b>Privacy:</b>
        <Row>
            <Col xs={12} lg={4} class="mb-3">
                <Label>Description:</Label>
                <Input type="select" bind:value={input.privacy.description_privacy}>
                    <option default>public</option>
                    <option>private</option>
                </Input>
            </Col>
            <Col xs={12} lg={4} class="mb-3">
                <Label>Name:</Label>
                <Input type="select" bind:value={input.privacy.name_privacy}>
                    <option default>public</option>
                    <option>private</option>
                </Input>
            </Col>
            <Col xs={12} lg={4} class="mb-3">
                <Label>Avatar:</Label>
                <Input type="select" bind:value={input.privacy.avatar_privacy}>
                    <option default>public</option>
                    <option>private</option>
                </Input>
            </Col>
            <Col xs={12} lg={4} class="mb-3">
                <Label>Birthday:</Label>
                <Input type="select" bind:value={input.privacy.birthday_privacy}>
                    <option default>public</option>
                    <option>private</option>
                </Input>
            </Col>
            <Col xs={12} lg={4} class="mb-3">
                <Label>Pronouns:</Label>
                <Input type="select" bind:value={input.privacy.pronoun_privacy}>
                    <option default>public</option>
                    <option>private</option>
                </Input>
            </Col>
            <Col xs={12} lg={4} class="mb-3">
                <Label>Visibility:</Label>
                <Input type="select" bind:value={input.privacy.visibility}>
                    <option default>public</option>
                    <option>private</option>
                </Input>
            </Col>
            <Col xs={12} lg={4} class="mb-3">
                <Label>Metadata:</Label>
                <Input type="select" bind:value={input.privacy.metadata_privacy}>
                    <option default>public</option>
                    <option>private</option>
                </Input>
            </Col>
        </Row>
        {/if}
        {#if proxyTagMode}
        <hr/>
        <b>Proxy tags:</b>
        <Row class="mb-2">
            {#each input.proxy_tags as proxyTag, index (index)}
            <Col xs={12} lg={4} class="mb-2">
                <InputGroup>
                    <Input style="resize: none; height: 1em" type="textarea" bind:value={proxyTag.prefix} />
                    <Input disabled value="text"/>
                    <Input style="resize: none; height: 1em" type="textarea" bind:value={proxyTag.suffix}/>
                </InputGroup>
            </Col>
            {/each}
            <Col xs={12} lg={4} class="mb-2">
                <Button class="w-100" color="secondary" on:click={() => {input.proxy_tags.push({prefix: "", suffix: ""}); input.proxy_tags = input.proxy_tags;}}>New</Button>
            </Col>
        </Row>
        {/if}
        <hr/>
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
            <textarea class="form-control" bind:value={input.description} maxlength={1000} use:autosize placeholder={input.description}/>
        </div>
        {#if !loading && input.name}<Button style="flex: 0" color="primary" on:click={submit}>Submit</Button>
        {:else if !input.name }<Button style="flex: 0" color="primary" disabled>Submit</Button>
        {:else}<Button style="flex: 0" color="primary" disabled><Spinner size="sm"/></Button>{/if}
    </AccordionItem>
</Accordion>