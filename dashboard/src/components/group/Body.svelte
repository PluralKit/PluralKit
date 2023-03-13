<script lang="ts">
    import { tick } from 'svelte';
    import { Row, Col, Modal, Image, Button, CardBody, ModalHeader, ModalBody, ModalFooter, Spinner } from 'sveltestrap';
    import moment from 'moment';
    import parseMarkdown from '../../api/parse-markdown';
    import resizeMedia from '../../api/resize-media';
    import Edit from './Edit.svelte';
    import twemoji from 'twemoji';
    import Privacy from './Privacy.svelte';
    import MemberEdit from './MemberEdit.svelte';
    import { Link, useLocation } from 'svelte-navigator';

    import type { Member, Group } from '../../api/types';
    import AwaitHtml from '../common/AwaitHtml.svelte';
   
    export let group: Group;
    let editMode: boolean = false;
    let memberMode: boolean = false;
    export let isPublic: boolean;
    export let members: Member[] = [];
    export let isMainDash = true;
    export let isPage = false;

    let htmlDescriptionPromise: Promise<string>;
    $: if (group.description) {
        htmlDescriptionPromise = parseMarkdown(group.description, { parseTimestamps: true, embed: true });
    }

    let htmlDisplayNamePromise: Promise<string>;
    $: if (group.display_name) htmlDisplayNamePromise = parseMarkdown(group.display_name, { parseTimestamps: true, embed: true });

    let settings = JSON.parse(localStorage.getItem("pk-settings"));
    let descriptionElement: any;
    let displayNameElement: any;

    $: if (settings && settings.appearance.twemoji) {
        if (descriptionElement) twemoji.parse(descriptionElement, { base: 'https://cdn.jsdelivr.net/gh/twitter/twemoji@14.0.2/assets/' });
        if (displayNameElement) twemoji.parse(displayNameElement, { base: 'https://cdn.jsdelivr.net/gh/twitter/twemoji@14.0.2/assets/' });
    };

    let created = moment(group.created).format("MMM D, YYYY");

    let bannerOpen = false;
    const toggleBannerModal = () => (bannerOpen = !bannerOpen);

    let privacyOpen = false;
    const togglePrivacyModal = () => (privacyOpen = !privacyOpen);

    async function focus(el) {
        await tick();
        el.focus();
    }

    let location = useLocation()
    let pathName = $location.pathname;

    function getGroupPageUrl(randomizer?: boolean) {
        let str: string;
        if (pathName.startsWith("/dash")) str = "/dash";
        else str = "/profile";

        str += `/g/${group.id}`;

        if (randomizer) str += "/random";

        return str;
    }
</script>

<CardBody style="border-left: 4px solid #{settings && settings.appearance.color_background ? isPage ? "" : group.color : group.color }; margin: -1rem">
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
        <b>Display Name:</b> <span bind:this={displayNameElement}><AwaitHtml htmlPromise={htmlDisplayNamePromise} /></span>
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
        <b>Banner:</b> <Button size="sm" color="secondary" on:click={toggleBannerModal} aria-label="view group banner">View</Button>
        <Modal isOpen={bannerOpen} toggle={toggleBannerModal}>
            <div slot="external" on:click={toggleBannerModal} style="height: 100%; width: max-content; max-width: 100%; margin-left: auto; margin-right: auto; display: flex;">
                <img class="img-thumbnail d-block m-auto" src={group.banner} tabindex={0} alt={`Group ${group.name} banner (full size)`} use:focus/>
            </div>
        </Modal>
    </Col>
    {/if}
    {#if group.privacy}
    <Col xs={12} lg={4} class="mb-2">
        <b>Privacy:</b> <Button size="sm" color="secondary" on:click={togglePrivacyModal} aria-label="edit group privacy">Edit</Button>
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
<div class="mt-2 mb-3 description" bind:this={descriptionElement}>
    <b>Description:</b><br />
    <AwaitHtml htmlPromise={htmlDescriptionPromise} />
</div>
{#if (group.banner && ((settings && settings.appearance.banner_bottom) || !settings))}
<img on:click={toggleBannerModal} src={resizeMedia(group.banner, [1200, 480])} alt="group banner" class="w-100 mb-3 rounded" style="max-height: 13em; object-fit: cover; cursor: pointer"/>
{/if}

{#if !isPublic}
<Button style="flex: 0" class="link-button" color="primary" on:click={() => editMode = true} aria-label="edit group information">Edit</Button> 
    {#if isMainDash}
    <Button class="link-button" style="flex: 0" color="secondary" on:click={() => memberMode = true} aria-label="edit group members">Members</Button>
    {/if}
{/if}

{#if !isPage}
    <Link to={getGroupPageUrl()}><button class="link-button button-right btn btn-primary" tabindex={-1} aria-label="view group page">View page</button></Link>
    {:else if !isPublic}
    <Link to="/dash?tab=groups"><button class="link-button button-right btn btn-primary" tabindex={-1} aria-label="view group system">View system</button></Link>
{/if}
<Link to={getGroupPageUrl(true)}><button class="link-button button-right btn btn-secondary" style={isPublic ? "float: none !important; margin-left: 0;" : ""} tabindex={-1} aria-label="randomize group members">Randomize group</button></Link>

{:else if editMode}
<Edit on:update on:deletion bind:group bind:editMode />
{:else if memberMode}
    <MemberEdit on:updateGroupMembers bind:group bind:memberMode bind:members />
{/if}
</CardBody>

<style>
    .link-button {
        width: 100%;
        margin-bottom: 0.2em;
    }

    @media (min-width: 992px) {
        .link-button {
            width: max-content;
            margin-bottom: 0;
        }

        .button-right {
            float: right;
            margin-left: 0.25em;
        }
    }
</style>
