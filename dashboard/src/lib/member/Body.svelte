<script lang="ts">
    import { tick } from 'svelte';
    import { Row, Col, Modal, Image, Button, CardBody, ModalHeader, ModalBody } from 'sveltestrap';
    import moment from 'moment';
    import { toHTML } from 'discord-markdown';
    import parseTimestamps from '../../api/parse-timestamps';
    import twemoji from 'twemoji';

    import GroupEdit from './GroupEdit.svelte';
    import Edit from './Edit.svelte';
    import Privacy from './Privacy.svelte';
    import ProxyTags from './ProxyTags.svelte';

    import { Member, Group } from '../../api/types';
    import { Link, useLocation } from 'svelte-navigator';

    export let groups: Group[] = [];
    export let member: Member;
    export let isPublic: boolean = false;
    export let isPage: boolean = false;
    export let isMainDash = true;

    let editMode: boolean = false;
    let groupMode: boolean = false;

    let htmlDescription: string;
    $: if (member.description) { 
        htmlDescription = toHTML(parseTimestamps(member.description), {embed: true});
    } else {
        htmlDescription = "(no description)";
    }

    let htmlPronouns: string;
    $: if (member.pronouns) { 
        htmlPronouns = toHTML(parseTimestamps(member.pronouns), {embed: true});
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

    let proxyOpen = false;
    const toggleProxyModal = () => (proxyOpen = !proxyOpen);

    let created = moment(member.created).format("MMM D, YYYY");
    let birthday: string;
    $: {member.birthday; 
        if (member.birthday) birthday = moment(member.birthday, "YYYY-MM-DD").format("MMM D, YYYY");
    }

    $: trimmedBirthday = birthday && birthday.endsWith(', 0004') ? trimmedBirthday = birthday.replace(', 0004', '') : birthday; 
    
    async function focus(el) {
        await tick();
        el.focus();
    }

    let location = useLocation()
    let pathName = $location.pathname;

    function getMemberPageUrl() {
        let str: string;
        if (pathName.startsWith("/dash")) str = "/dash";
        else str = "/profile";

        str += `/m/${member.id}`;

        return str;
    }

    function getSystemPageUrl() {
        let str: string;
        if (pathName.startsWith("/dash")) str = "/dash";
        else {
            str = "/profile";
            str += `/s/${member.system}`;
        }
        str += "?tab=members";

        return str;
    }
</script>

<CardBody style="border-left: 4px solid #{settings && settings.appearance.color_background ? isPage ? "" : member.color : member.color }; margin: -1rem">
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
            <b>Birthday:</b> {trimmedBirthday}
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
        <Col xs={12} lg={4} class="mb-2">
            <b>Banner:</b> <Button size="sm" color="secondary" on:click={toggleBannerModal} aria-label="view member banner">View</Button>
            <Modal isOpen={bannerOpen} toggle={toggleBannerModal}>
                <div slot="external" on:click={toggleBannerModal} style="height: 100%; width: max-content; max-width: 100%; margin-left: auto; margin-right: auto; display: flex;">
                    <img class="img-thumbnail d-block m-auto" src={member.banner} tabindex={0} alt={`Member ${member.name} banner (full size)`} use:focus/>
                </div>
            </Modal>
        </Col>
        {/if}
        {#if member.privacy && !isPublic}
        <Col xs={12} lg={4} class="mb-2">
            <b>Privacy:</b> <Button size="sm" color="secondary" on:click={togglePrivacyModal} aria-label="edit member privacy">Edit</Button>
            <Modal size="lg" isOpen={privacyOpen} toggle={togglePrivacyModal}>
                <ModalHeader toggle={togglePrivacyModal}>
                    Edit privacy
                </ModalHeader>
                    <ModalBody>
                        <Privacy bind:member bind:privacyOpen/>
                    </ModalBody>
            </Modal>
        </Col>
        {/if}
        {#if member.proxy_tags && !isPublic}
        <Col xs={12} lg={4} class="mb-2">
            <b>Proxy Tags:</b> <Button size="sm" color="secondary" on:click={toggleProxyModal} aria-label="edit member proxy tags">Edit</Button>
            <Modal size="lg" isOpen={proxyOpen} toggle={toggleProxyModal}>
                <ModalHeader toggle={toggleProxyModal}>
                    Edit proxy tags
                </ModalHeader>
                    <ModalBody>
                        <ProxyTags bind:member bind:proxyOpen/>
                    </ModalBody>
            </Modal>
        </Col>
        {/if}
    </Row>
    <div class="my-2 mb-3 description" bind:this={descriptionElement}>
        <b>Description:</b><br />
        {@html htmlDescription && htmlDescription}
    </div>
    {#if (member.banner && ((settings && settings.appearance.banner_bottom) || !settings))}
    <img src={member.banner} alt="member banner" class="w-100 mb-3 rounded" style="max-height: 17em; object-fit: cover"/>
    {/if}
    {#if !isPublic}
    <Button style="flex: 0" color="primary" on:click={() => editMode = true} aria-label="edit member information">Edit</Button>
    {#if isMainDash}<Button style="flex: 0" color="secondary" on:click={() => groupMode = true} aria-label="edit member groups">Groups</Button>{/if}
    {/if}
    {#if !isPage}
    <Link to={getMemberPageUrl()}><Button style="flex: 0; {!isPublic && "float: right;"}" color="primary" tabindex={-1} aria-label="view member page">View page</Button></Link>
    {:else}
    <Link to={getSystemPageUrl()}><Button style="flex: 0; {!isPublic && "float: right;"}" color="primary" tabindex={-1} aria-label="view member's system">View system</Button></Link>
    {/if}
    {:else if editMode}
        <Edit on:deletion bind:member bind:editMode />
    {:else if groupMode}
        <GroupEdit bind:member bind:groups bind:groupMode />
    {/if}
    </CardBody>
