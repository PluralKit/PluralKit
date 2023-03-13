<script lang="ts">
    import { Row, Col, Modal, Image, Button } from 'sveltestrap';
    import moment from 'moment';
    import parseMarkdown from '../../api/parse-markdown';
    import resizeMedia from '../../api/resize-media';
    import twemoji from 'twemoji';

    import type { System } from '../../api/types';
    import AwaitHtml from '../common/AwaitHtml.svelte';

    export let user: System;
    export let editMode: boolean;
    export let isPublic: boolean;

    let htmlDescriptionPromise: Promise<string>;
    let htmlNamePromise: Promise<string>;
    let htmlPronounsPromise: Promise<string>;

    if (user.description) {
        htmlDescriptionPromise = parseMarkdown(user.description, { embed: true, parseTimestamps: true });
    } else {
        htmlDescriptionPromise = Promise.resolve("(no description)");
    }

    if (user.name) {
        htmlNamePromise = parseMarkdown(user.name);
    }

    if (user.pronouns) {
        htmlPronounsPromise = parseMarkdown(user.pronouns);
    }

    let created = moment(user.created).format("MMM D, YYYY");

    let bannerOpen = false;
    const toggleBannerModal = () => (bannerOpen = !bannerOpen);

    let settings = JSON.parse(localStorage.getItem("pk-settings"));
    let descriptionElement: any;
    let nameElement: any;
    let tagElement: any;
    let pronounElement: any;

    $: if (settings && settings.appearance.twemoji) {
        if (descriptionElement) twemoji.parse(descriptionElement, { base: 'https://cdn.jsdelivr.net/gh/twitter/twemoji@14.0.2/assets/' });
        if (nameElement) twemoji.parse(nameElement, { base: 'https://cdn.jsdelivr.net/gh/twitter/twemoji@14.0.2/assets/' });
        if (tagElement) twemoji.parse(tagElement, { base: 'https://cdn.jsdelivr.net/gh/twitter/twemoji@14.0.2/assets/' });
        if (pronounElement) twemoji.parse(pronounElement, { base: 'https://cdn.jsdelivr.net/gh/twitter/twemoji@14.0.2/assets/' });
    }

</script>

<Row>
    {#if user.id}
    <Col xs={12} lg={4} class="mb-2">
        <b>ID:</b> {user.id}
    </Col>
    {/if}
    {#if user.name}
    <Col xs={12} lg={4} class="mb-2">
        <span  bind:this={nameElement}><b>Name:</b> <AwaitHtml htmlPromise={htmlNamePromise} /></span>
    </Col>
    {/if}
    {#if user.tag}
    <Col xs={12} lg={4} class="mb-2">
        <span bind:this={tagElement}><b>Tag:</b> {user.tag}</span>
    </Col>
    {/if}
    {#if user.pronouns}
    <Col xs={12} lg={4} class="mb-2">
        <span bind:this={pronounElement}><b>Pronouns:</b> <AwaitHtml htmlPromise={htmlPronounsPromise} /></span>
    </Col>
    {/if}
    {#if user.created && !isPublic}
    <Col xs={12} lg={4} class="mb-2">
        <b>Created:</b> {created}
    </Col>
    {/if}
    {#if user.timezone && !isPublic}
    <Col xs={12} lg={4} class="mb-2">
        <b>Timezone:</b> {user.timezone}
    </Col>
    {/if}
    {#if user.color}
    <Col xs={12} lg={4} class="mb-2">
        <b>Color:</b> {user.color}
    </Col>
    {/if}
    {#if user.banner}
    <Col xs={12} lg={3} class="mb-2">
        <b>Banner:</b> <Button size="sm" color="secondary" on:click={toggleBannerModal} aria-label="view system banner">View</Button>
        <Modal isOpen={bannerOpen} toggle={toggleBannerModal}>
            <div slot="external" on:click={toggleBannerModal} style="height: 100%; width: max-content; max-width: 100%; margin-left: auto; margin-right: auto; display: flex;">
                <Image style="display: block; margin: auto;" src={user.banner} thumbnail alt="system banner" />
            </div>
        </Modal>
    </Col>
    {/if}
</Row>
<div class="my-2 description" bind:this={descriptionElement}>
    <b>Description:</b><br />
    <AwaitHtml htmlPromise={htmlDescriptionPromise} />
</div>
{#if (user.banner && ((settings && settings.appearance.banner_bottom) || !settings))}
<img on:click={toggleBannerModal} src={resizeMedia(user.banner, [1200, 480])} alt="system banner" class="w-100 mb-3 rounded" style="max-height: 13em; object-fit: cover; cursor: pointer;"/>
{/if}
{#if !isPublic}
<Button style="flex: 0" color="primary" on:click={() => editMode = true} aria-label="edit system information">Edit</Button>
{/if}
