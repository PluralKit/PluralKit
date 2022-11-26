<script lang="ts">
    import {tick } from "svelte";
    import { Col, Row, Input, Label, Button, Alert, Spinner, InputGroup } from "sveltestrap";

    import { Member } from '../../api/types';
    import api from '../../api';

    let loading: boolean;
    export let proxyOpen: boolean;
    export let member: Member;
    const toggleProxyModal = () => (proxyOpen = !proxyOpen);

    let err: string;

    let input = member.proxy_tags;

    async function submit() {
        err = null;
        if (input.some(tag => tag.prefix && tag.suffix && tag.prefix.length + tag.suffix.length + 4 > 100)) {
            err = "One of your proxy tags is too long (prefix + 'text' + suffix must be shorter than 100 characters). Please shorten this tag, or remove it."
            return;
        }

        let data: Member = {proxy_tags: input};
        loading = true;

        try {
            let res = await api().members(member.id).patch({data});
            member = res;
            err = null;
            loading = false;
            toggleProxyModal();
        } catch (error) {
            console.log(error);
            err = error.message;
            loading = false;
        }
    }

    async function focus(el, first) {
        if (first) {
        await tick();
        el.focus();
        }
    }
</script>

{#if err}
<Alert color="danger">{err}</Alert>
{/if}
<Row class="mb-2">
    {#each input as proxyTag, index (index)}
    <Col xs={12} lg={6} class="mb-2">
        <InputGroup>
            <textarea class="form-control" style="resize: none; height: 1em" bind:value={proxyTag.prefix} use:focus={index === 0 ? true : false} aria-label="proxy tag prefix"/>
            <Input disabled value="text"/>
            <Input style="resize: none; height: 1em" type="textarea" bind:value={proxyTag.suffix} aria-label="proxy tag suffix"/>
        </InputGroup>
    </Col>
    {/each}
    <Col xs={12} lg={6} class="mb-2">
        <button class="w-100 btn btn-secondary" use:focus={member.proxy_tags.length > 0 ? false : true} on:click={() => {input.push({prefix: "", suffix: ""}); input = input;}}>New</button>
    </Col>
</Row>
{#if !loading}<Button style="flex 0" color="primary" on:click={submit} aria-label="submit proxy tags">Submit</Button> <Button style="flex: 0" color="secondary" on:click={toggleProxyModal} aria-label="go back">Back</Button>
{:else}<Button style="flex 0" color="primary" disabled aria-label="submit proxy tags"><Spinner size="sm"/></Button> <Button style="flex: 0" color="secondary" disabled aria-label="go back">Back</Button>
{/if}