<script lang="ts">
    import { Row, Col, Modal, Image, Button } from 'sveltestrap';
    import moment from 'moment';
    import { toHTML } from 'discord-markdown';
    import parseTimestamps from '../../api/parse-timestamps';
    import twemoji from 'twemoji';

    import { System } from '../../api/types';
   
    export let user: System;
    export let editMode: boolean;
    export let isPublic: boolean;

    let htmlDescription: string;
    let htmlName: string;
    if (user.description) { 
        htmlDescription = toHTML(parseTimestamps(user.description), {embed: true});
    } else {
        htmlDescription = "(no description)";
    }

    if (user.name) {
        htmlName = toHTML(user.name);
    }

    let created = moment(user.created).format("MMM D, YYYY");

    let bannerOpen = false;
    const toggleBannerModal = () => (bannerOpen = !bannerOpen);

    let settings = JSON.parse(localStorage.getItem("pk-settings"));
    let descriptionElement: any;
    let nameElement: any;
    let tagElement: any;

    $: if (settings && settings.appearance.twemoji) {
        if (descriptionElement) twemoji.parse(descriptionElement);
        if (nameElement) twemoji.parse(nameElement);
        if (tagElement) twemoji.parse(tagElement);
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
        <span  bind:this={nameElement}><b>Name:</b> {@html htmlName}</span>
    </Col>
    {/if}
    {#if user.tag}
    <Col xs={12} lg={4} class="mb-2">
        <span bind:this={tagElement}><b>Tag:</b> {user.tag}</span>
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
    {@html htmlDescription}
</div>
{#if (user.banner && ((settings && settings.appearance.banner_bottom) || !settings))}
<img src={user.banner} alt="system banner" class="w-100 mb-3 rounded" style="max-height: 12em; object-fit: cover"/>
{/if}
{#if !isPublic}
<Button style="flex: 0" color="primary" on:click={() => editMode = true} aria-label="edit system information">Edit</Button>
{/if}
