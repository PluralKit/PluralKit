<script lang="ts">
    import { Modal, Card, CardHeader, CardBody, CardTitle, Image, ModalHeader, Col, Row, Button } from 'sveltestrap';
    import CardsHeader from '../CardsHeader.svelte';
    import { parseMarkdown } from '../../functions';

    export let user;
    $: htmlDescription = parseMarkdown(user.description);

    let bannerOpen = false;
    const toggleBannerModal = () => (bannerOpen = !bannerOpen);
</script>

<Card>
    <CardsHeader bind:item={user}/>
    <CardBody style="border-left: 4px solid #{user.color}">
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
            <div class="mt-2">
                <b>Description:</b><br />
                {@html htmlDescription}
            </div>
        </Row>
    </CardBody>
</Card>