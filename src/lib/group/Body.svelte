<script lang="ts">
    import { Row, Col, Modal, Image, Button, CardBody } from 'sveltestrap';
    import moment from 'moment';
    import { toHTML } from 'discord-markdown';
    import type Group from '../../api/group';
   
    export let group: Group;
    let editMode: boolean;
    export let isPublic: boolean;

    let htmlDescription: string;
    if (group.description) { 
        htmlDescription = toHTML(group.description, {embed: true});
    } else {
        htmlDescription = "(no description)";
    }

    let created = moment(group.created).format("MMM D, YYYY");

    let bannerOpen = false;
    const toggleBannerModal = () => (bannerOpen = !bannerOpen);

    let settings = JSON.parse(localStorage.getItem("pk-settings"));
</script>

<CardBody style="border-left: 4px solid #{group.color}; margin: -1rem -1.25rem">
{#if !editMode }
<Row>
    {#if group.id}
    <Col xs={12} lg={4} class="mb-2">
        <b>ID:</b> {group.id}
    </Col>
    {/if}
    {#if group.name}
    <Col xs={12} lg={4} class="mb-2">
        <b>Name:</b> {group.name}
    </Col>
    {/if}
    {#if group.display_name}
    <Col xs={12} lg={4} class="mb-2">
        <b>Display Name:</b> {group.display_name}
    </Col>
    {/if}
    {#if group.created && !isPublic}
    <Col xs={12} lg={4} class="mb-2">
        <b>Created:</b> {created}
    </Col>
    {/if}
    {#if group.color}
    <Col xs={12} lg={4} class="mb-2">
        <b>Color:</b> {group.color}
    </Col>
    {/if}
    {#if group.banner}
    <Col xs={12} lg={3} class="mb-2">
        <b>Banner:</b> <Button size="sm" color="light" on:click={toggleBannerModal}>View</Button>
        <Modal isOpen={bannerOpen} toggle={toggleBannerModal}>
            <div slot="external" on:click={toggleBannerModal} style="height: 100%; width: max-content; max-width: 100%; margin-left: auto; margin-right: auto; display: flex;">
                <Image style="display: block; margin: auto;" src={group.banner} thumbnail alt="Your system banner" />
            </div>
        </Modal>
    </Col>
    {/if}
</Row>
<div class="my-2 description">
    <b>Description:</b><br />
    {@html htmlDescription}
</div>
{#if (group.banner && ((settings && settings.appearance.banner_bottom) || !settings))}
<img src={group.banner} alt="your system banner" class="w-100 mb-3 rounded" style="max-height: 12em; object-fit: cover"/>
{/if}
{#if !isPublic}
<Button style="flex: 0" color="primary" on:click={() => editMode = true}>Edit</Button>
{/if}
{:else}
woohoo editing goes here
{/if}
</CardBody>