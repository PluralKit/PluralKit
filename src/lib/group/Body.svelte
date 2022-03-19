<script lang="ts">
    import { Row, Col, Modal, Image, Button, CardBody, ModalHeader, ModalBody, ModalFooter, Spinner } from 'sveltestrap';
    import moment from 'moment';
    import { toHTML } from 'discord-markdown';
    import Edit from './Edit.svelte';
    import twemoji from 'twemoji';
    import Privacy from './Privacy.svelte';
    import MemberEdit from './MemberEdit.svelte';
    import { navigate } from 'svelte-navigator';

    import { Member, Group } from '../../api/types';
   
    export let group: Group;
    let editMode: boolean = false;
    let memberMode: boolean = false;
    export let isPublic: boolean;
    export let members: Member[] = [];
    export let isMainDash = true;
    export let isPage = false;

    let htmlDescription: string;
    $: if (group.description) { 
        htmlDescription = toHTML(group.description, {embed: true});
    } else {
        htmlDescription = "(no description)";
    }
    let htmlDisplayName: string;
    $: if (group.display_name) htmlDisplayName = toHTML(group.display_name)

    let settings = JSON.parse(localStorage.getItem("pk-settings"));
    let descriptionElement: any;
    let displayNameElement: any;

    $: if (settings && settings.appearance.twemoji) {
        if (descriptionElement) twemoji.parse(descriptionElement);
        if (displayNameElement) twemoji.parse(displayNameElement);
    };

    let created = moment(group.created).format("MMM D, YYYY");

    let bannerOpen = false;
    const toggleBannerModal = () => (bannerOpen = !bannerOpen);

    let privacyOpen = false;
    const togglePrivacyModal = () => (privacyOpen = !privacyOpen);

</script>

<CardBody style="border-left: 4px solid #{group.color}; margin: -1rem -1.25rem">
{#if !editMode && !memberMode}
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
        <b>Display Name:</b> <span bind:this={displayNameElement}>{@html htmlDisplayName}</span>
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
    {#if group.privacy}
    <Col xs={12} lg={4} class="mb-2">
        <b>Privacy:</b> <Button size="sm" color="secondary" on:click={togglePrivacyModal}>Edit</Button>
        <Modal size="lg" isOpen={privacyOpen} toggle={togglePrivacyModal}>
            <ModalHeader toggle={togglePrivacyModal}>
               Edit privacy
            </ModalHeader>
                <ModalBody>
                    <Privacy on:update bind:group bind:privacyOpen={privacyOpen}/>
                </ModalBody>
        </Modal>
    </Col>
    {/if}
</Row>
<div class="my-2 description" bind:this={descriptionElement}>
    <b>Description:</b><br />
    {@html htmlDescription && htmlDescription}
</div>
{#if (group.banner && ((settings && settings.appearance.banner_bottom) || !settings))}
<img src={group.banner} alt="your system banner" class="w-100 mb-3 rounded" style="max-height: 12em; object-fit: cover"/>
{/if}
{#if !isPublic}
<Button style="flex: 0" color="primary" on:click={() => editMode = true}>Edit</Button> 
{#if isMainDash}<Button style="flex: 0" color="secondary" on:click={() => memberMode = true}>Members</Button>{/if}
{/if}
{#if !isPage}
    <Button style="flex: 0; {!isPublic && "float: right;"}" color="primary" on:click={() => navigate(isPublic ? `/profile/g/${group.id}` : `/dash/g/${group.id}`)}>View page</Button>
    {:else if !isPublic}
    <Button style="flex: 0; {!isPublic && "float: right;"}" color="primary" on:click={() => navigate("/dash?tab=groups")}>View system</Button>
    {/if}
{:else if editMode}
<Edit on:deletion on:update bind:group bind:editMode />
{:else if memberMode}
    <MemberEdit on:update bind:group bind:memberMode bind:members />
{/if}
</CardBody>