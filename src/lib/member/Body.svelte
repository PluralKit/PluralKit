<script lang="ts">
    import { Row, Col, Modal, Image, Button, CardBody, ModalHeader, ModalBody, Spinner } from 'sveltestrap';
    import moment from 'moment';
    import { toHTML } from 'discord-markdown';
    import twemoji from 'twemoji';

    import type Member from '../../api/member';
    import type Group from '../../api/group';
    import GroupEdit from './GroupEdit.svelte';

    export let groups: Group[] = [];
    export let member: Member;
    export let loading: boolean;
    export let isPublic: boolean = false;

    let editMode: boolean = false;
    let groupMode: boolean = false;
    let privacyLoading = false;

    let htmlDescription: string;
    $: if (member.description) { 
        htmlDescription = toHTML(member.description, {embed: true});
    } else {
        htmlDescription = "(no description)";
    }

    let htmlPronouns: string;
    $: if (member.pronouns) { 
        htmlPronouns = toHTML(member.pronouns, {embed: true});
    }

    let settings = JSON.parse(localStorage.getItem("pk-settings"));
    let descriptionElement: any;
    let displayNameElement: any;
    let pronounElement: any;

    $: if (settings && settings.appearance.twemoji) {
        if (descriptionElement) twemoji.parse(descriptionElement);
        if (displayNameElement) twemoji.parse(displayNameElement);
        if (pronounElement) twemoji.parse(pronounElement);
    };

    let bannerOpen = false;
    const toggleBannerModal = () => (bannerOpen = !bannerOpen);

    let privacyOpen = false;
    const togglePrivacyModal = () => (privacyOpen = !privacyOpen);

    let created = moment(member.created).format("MMM D, YYYY");
    let birthday: string;
    if (member.birthday) birthday = moment(member.birthday).format("MMM D, YYYY");

</script>

<CardBody style="border-left: 4px solid #{member.color}; margin: -1rem -1.25rem">
    {#if !editMode && !groupMode}
    <Row>
        {#if member.id}
        <Col xs={12} lg={4} class="mb-2">
            <b>ID:</b> {member.id}
        </Col>
        {/if}
        {#if member.name}
        <Col xs={12} lg={4} class="mb-2">
            <b>Name:</b> {member.name}
        </Col>
        {/if}
        {#if member.display_name}
        <Col xs={12} lg={4} class="mb-2">
            <b>Display Name:</b> <span bind:this={displayNameElement}>{member.display_name}</span>
        </Col>
        {/if}
        {#if member.pronouns}
        <Col xs={12} lg={4} class="mb-2">
            <b>Pronouns:</b> <span bind:this={pronounElement}>{@html htmlPronouns}</span>
        </Col>
        {/if}
        {#if member.birthday}
        <Col xs={12} lg={4} class="mb-2">
            <b>Birthday:</b> {birthday}
        </Col>
        {/if}
        {#if member.created}
        <Col xs={12} lg={4} class="mb-2">
            <b>Created:</b> {created}
        </Col>
        {/if}
        {#if member.color}
        <Col xs={12} lg={4} class="mb-2">
            <b>Color:</b> {member.color}
        </Col>
        {/if}
        {#if member.banner}
        <Col xs={12} lg={3} class="mb-2">
            <b>Banner:</b> <Button size="sm" color="secondary" on:click={toggleBannerModal}>View</Button>
            <Modal isOpen={bannerOpen} toggle={toggleBannerModal}>
                <div slot="external" on:click={toggleBannerModal} style="height: 100%; width: max-content; max-width: 100%; margin-left: auto; margin-right: auto; display: flex;">
                    <Image style="display: block; margin: auto;" src={member.banner} thumbnail alt="Your system banner" />
                </div>
            </Modal>
        </Col>
        {/if}
        {#if member.privacy}
        <Col xs={12} lg={4} class="mb-2">
            <b>Privacy:</b> <Button size="sm" color="secondary" on:click={togglePrivacyModal}>Edit</Button>
            <Modal size="lg" isOpen={privacyOpen} toggle={togglePrivacyModal}>
                <ModalHeader toggle={togglePrivacyModal}>
                    {#if privacyLoading}<div style="float: left; width: 3rem;"><Spinner color="primary" /></div>{/if} Edit privacy
                </ModalHeader>
                    <ModalBody>
                        whoops! this is yet to be added.
                    </ModalBody>
            </Modal>
        </Col>
        {/if}
    </Row>
    <div class="my-2 mb-3 description" bind:this={descriptionElement}>
        <b>Description:</b><br />
        {@html htmlDescription}
    </div>
    {#if (member.banner && ((settings && settings.appearance.banner_bottom) || !settings))}
    <img src={member.banner} alt="your system banner" class="w-100 mb-3 rounded" style="max-height: 17em; object-fit: cover"/>
    {/if}
    {#if !isPublic}
    <Button style="flex: 0" color="primary" on:click={() => editMode = true}>Edit</Button> <Button style="flex: 0" color="secondary" on:click={() => groupMode = true}>Groups</Button>
    {/if}
    {:else if editMode}
    editing tba o_o
    {:else if groupMode}
        <GroupEdit bind:loading bind:member bind:groups bind:groupMode />
    {/if}
    </CardBody>