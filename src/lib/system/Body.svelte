<script lang="ts">
    import { Row, Col, Modal, Image, Button } from 'sveltestrap';
    import moment from 'moment';
    import { toHTML } from 'discord-markdown';
    import type Sys from '../../api/system';
   
    export let user: Sys;
    export let editMode: boolean;

    let htmlDescription: string;
    if (user.description) { 
        htmlDescription = toHTML(user.description, {embed: true});
    } else {
        htmlDescription = "(no description)";
    }

    let created = moment(user.created).format("MMM D, YYYY");

    let bannerOpen = false;
    const toggleBannerModal = () => (bannerOpen = !bannerOpen);

    let settings = JSON.parse(localStorage.getItem("pk-settings"));
</script>

<Row>
    <Col xs={12} lg={4} class="mb-2">
        <b>ID:</b> {user.id}
    </Col>
    <Col xs={12} lg={4} class="mb-2">
        <b>Name:</b> {user.name}
    </Col>
    {#if user.tag}
    <Col xs={12} lg={4} class="mb-2">
        <b>Tag:</b> {user.tag}
    </Col>
    <Col xs={12} lg={4} class="mb-2">
        <b>Created:</b> {created}
    </Col>
    {/if}
    <Col xs={12} lg={4} class="mb-2">
        <b>Timezone:</b> {user.timezone}
    </Col>
    {#if user.color}
    <Col xs={12} lg={4} class="mb-2">
        <b>Color:</b> {user.color}
    </Col>
    {/if}
    {#if user.banner}
    <Col xs={12} lg={3} class="mb-2">
        <b>Banner:</b> <Button size="sm" color="light" on:click={toggleBannerModal}>View</Button>
        <Modal isOpen={bannerOpen} toggle={toggleBannerModal}>
            <div slot="external" on:click={toggleBannerModal} style="height: 100%; width: max-content; max-width: 100%; margin-left: auto; margin-right: auto; display: flex;">
                <Image style="display: block; margin: auto;" src={user.banner} thumbnail alt="Your system banner" />
            </div>
        </Modal>
    </Col>
    {/if}
</Row>
<div class="my-2">
    <b>Description:</b><br />
    {@html htmlDescription}
</div>
{#if user.banner && settings && settings.appearance.banner_bottom}
<img src={user.banner} alt="your system banner" class="w-100 mb-3 rounded" style="max-height: 12em; object-fit: cover"/>
{/if}
<Button style="flex: 0" color="primary" on:click={() => editMode = true}>Edit</Button>