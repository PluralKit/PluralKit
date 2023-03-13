<script lang="ts">
    import { createEventDispatcher } from 'svelte';
    import { Card, CardHeader, CardTitle, Modal, Button, ListGroup, ListGroupItem, Input, Alert, Label, Spinner, Row, Col, Tooltip } from 'sveltestrap';
    import AwaitHtml from '../common/AwaitHtml.svelte';
    import parseMarkdown from '../../api/parse-markdown';
    import twemoji from 'twemoji';
    import { Link } from 'svelte-navigator';
    import { autoresize } from 'svelte-textarea-autoresize';

    import FaEdit from 'svelte-icons/fa/FaEdit.svelte'
    import FaInfoCircle from 'svelte-icons/fa/FaInfoCircle.svelte'
    import FaUsers from 'svelte-icons/fa/FaUsers.svelte'
    import FaTimes from 'svelte-icons/fa/FaTimes.svelte'
    import FaCheck from 'svelte-icons/fa/FaCheck.svelte'

    import type { Group, Member} from '../../api/types';
    import api from '../../api';
    import default_avatar from '../../assets/default_avatar.png';
    import resizeMedia from '../../api/resize-media';


    export let member: Member;
    export let searchBy: string;
    export let sortBy: string;
    export let groups: Group[];
    export let isPublic = false;
    export let isDash = false;

    let input: Member = JSON.parse(JSON.stringify(member));

    let view = "card";
    let err: string[] = [];
    let loading = false;
    let success = false;

    let settings = JSON.parse(localStorage.getItem("pk-settings"));
    
    let htmlNamePromise: Promise<string>;
    $: htmlDescPromise = member.description ? parseMarkdown(member.description, { embed: true }) : Promise.resolve("(no description)");
    $: htmlDisplayNamePromise = member.display_name ? parseMarkdown(member.display_name) : undefined;
    $: htmlPronounsPromise = member.pronouns ? parseMarkdown(member.pronouns, {embed: true}) : undefined;

    let nameElement: any;
    let descElement: any;
    let dnElement: any;
    let prnsElement: any;

    $: if (member.name) {
        if ((searchBy === "display name" || sortBy === "display name") && member.display_name) htmlNamePromise = parseMarkdown(member.display_name);
        else htmlNamePromise = parseMarkdown(member.name);
    }
    if (settings && settings.appearance.twemoji) {
        if (nameElement) twemoji.parse(nameElement, { base: 'https://cdn.jsdelivr.net/gh/twitter/twemoji@14.0.2/assets/' });
        if (descElement) twemoji.parse(descElement, { base: 'https://cdn.jsdelivr.net/gh/twitter/twemoji@14.0.2/assets/' });
        if (dnElement) twemoji.parse(dnElement, { base: 'https://cdn.jsdelivr.net/gh/twitter/twemoji@14.0.2/assets/' });
        if (prnsElement) twemoji.parse(prnsElement, { base: 'https://cdn.jsdelivr.net/gh/twitter/twemoji@14.0.2/assets/' });
    }

    let avatarOpen = false;
    const toggleAvatarModal = () => (avatarOpen = !avatarOpen);

    $: icon_url = member.avatar_url ? member.avatar_url : default_avatar;
    $: icon_url_resized = resizeMedia(icon_url);

    let altText = `member ${member.name} avatar`;
    
    let pageLink = isPublic ? `/profile/m/${member.id}` : `/dash/m/${member.id}`;
    
    const dispatch = createEventDispatcher();

    function update(member: Member) {
        dispatch('update', member);
    }

    async function submit() {
        let data = input;
        err = [];

        if (data.color && !/^#?[A-Fa-f0-9]{6}$/.test(input.color)) {
            err.push(`"${data.color}" is not a valid color, the color must be a 6-digit hex code. (example: #ff0000)`);
        } else if (data.color) {
            if (data.color.startsWith("#")) {
                data.color = input.color.slice(1, input.color.length);
            }
        }

        // trim all string fields
        Object.keys(data).forEach(k => data[k] = typeof data[k] == "string" ? data[k].trim() : data[k]);

        err = err;
        if (err.length > 0) return;

        loading = true;
        try {
            let res = await api().members(member.id).patch({data});
            update({...member, ...res});
            view = 'card';
        } catch (error) {
            console.log(error);
            err.push(error.message);
            err = err;
        }
        loading = false;
    }

    $: groupList = groups && groups.filter(g => g.members && g.members.includes(member.uuid)).sort((a, b) => a.name.localeCompare(b.name)) || [];
    let listGroupElements = [];

    $: if (view !== "edit") {
        err = [];
        success = false;
    }
</script>

<div class="mb-4 pb-3 card item" style={`${member.color && `border-bottom: 4px solid #${member.color}`}`}>
    <CardHeader>
        <CardTitle class="d-flex justify-content-center align-middle w-100 mb-0">
            <div class="icon d-inline-block">
                <slot name="icon" />
            </div>
            <span bind:this={nameElement} style="vertical-align: middle; margin-bottom: 0;"><AwaitHtml htmlPromise={htmlNamePromise} /> ({member.id})</span>
        </CardTitle>
    </CardHeader>
    <div class="card-body d-block hide-scrollbar" style="flex: 1; overflow: auto;">
        {#if view === "card"}
        <img style="cursor: pointer;" tabindex={0} on:keydown={(event) => {if (event.key === "Enter") {avatarOpen = true}}} on:click={toggleAvatarModal} class="rounded avatar mx-auto w-100 h-auto mb-2" src={icon_url_resized} alt={altText}/>
        <Modal isOpen={avatarOpen} toggle={toggleAvatarModal}>
            <div slot="external" on:click={toggleAvatarModal} style="height: 100%;  max-width: 640px; width: 100%; margin-left: auto; margin-right: auto; display: flex;">
                <img class="d-block m-auto img-thumbnail" src={icon_url} alt="Member avatar" tabindex={0}/>
            </div>
        </Modal>
        {#if member.display_name}
        <div class="text-center" bind:this={dnElement}><b><AwaitHtml htmlPromise={htmlDisplayNamePromise} /></b></div>
        {/if}
        {#if member.pronouns}
        <div class="text-center" bind:this={prnsElement}><AwaitHtml htmlPromise={htmlPronounsPromise} /></div>
        {/if}
        <hr style="min-height: 1px;"/>
        <div bind:this={descElement}>
            <AwaitHtml htmlPromise={htmlDescPromise} />
        </div>
        <hr style="min-height: 1px;"/>
        <Row>
            <Col xs={4} class="align-items-center justify-content-center">
                {#if !isPublic}<Button color="link" class="mt-2" style="height: 3.5rem;" on:click={() => {view = "edit"}} id={`member-${member.uuid}-edit-button-card`}><FaEdit/></Button>{/if}
            </Col>
            <Col xs={4} class="align-items-center justify-content-center">
                {#if !isPublic && isDash}<Button color="link" class="mt-2 text-reset" style="height: 3.5rem;" on:click={() => {view = "groups"}} id={`member-${member.uuid}-groups-button-card`}><FaUsers/></Button>{/if}
            </Col>
            <Col xs={4} class="align-items-center justify-content-center">
                <Link tabindex={-1} to={pageLink} class="text-reset"><Button color="link" class="mt-2 w-100 text-reset" style="height: 3.5rem;" id={`member-${member.uuid}-view-button-card`}><FaInfoCircle/></Button></Link> 
            </Col>
            {#if !isPublic}<Tooltip target={`member-${member.uuid}-edit-button-card`} placement="bottom">Edit member</Tooltip>{/if}
            {#if !isPublic && isDash}<Tooltip target={`member-${member.uuid}-groups-button-card`} placement="bottom">View groups</Tooltip>{/if}
            <Tooltip target={`member-${member.uuid}-view-button-card`} placement="bottom">View page</Tooltip>
        </Row>
        {:else if view === "groups"}
        {#if groupList.length > 0}
        <b class="d-block text-center w-100">Groups</b>
        <hr style="min-height: 1px"/>
        <ListGroup>
            {#each groupList as group, index (group.id)}
            <ListGroupItem class="d-flex"><span bind:this={listGroupElements[index]}><span><b><AwaitHtml htmlPromise={parseMarkdown(group.name)} /></b> (<code>{group.id}</code>)</span></ListGroupItem>
            {/each}
        </ListGroup>
        {:else}
        <b class="d-block text-center w-100">This member is in no groups.</b>
        {/if}
        <hr style="min-height: 1px"/>
        <Row>
            <Col xs={4} class="align-items-center justify-content-center">
                <Button color="link" class="mt-2" style="height: 3.5rem;" on:click={() => {view = "edit"}} id={`member-${member.uuid}-edit-button-groups`}><FaEdit/></Button>
            </Col>
            <Col xs={4} class="align-items-center justify-content-center">
                <Button color="link" class="mt-2 text-reset" style="height: 3.5rem;" on:click={() => {view = "card"}} id={`member-${member.uuid}-back-button-groups`}><FaTimes/></Button>
            </Col>
            <Col xs={4} class="align-items-center justify-content-center">
                <Link tabindex={-1} to={`./m/${member.id}`} class="text-reset"><Button color="link" class="mt-2 w-100 text-reset" style="height: 3.5rem;" id={`member-${member.uuid}-view-button-groups`}><FaInfoCircle/></Button></Link> 
            </Col>
            <Tooltip target={`member-${member.uuid}-edit-button-groups`} placement="bottom">Edit member</Tooltip>
            <Tooltip target={`member-${member.uuid}-back-button-groups`} placement="bottom">Back to info</Tooltip>
            <Tooltip target={`member-${member.uuid}-view-button-groups`} placement="bottom">View page</Tooltip>
        </Row>
        {:else if view === "edit"}
            <Label>Name:</Label>
            <Input class="mb-2" bind:value={input.name} maxlength={100} type="text" placeholder={member.name} aria-label="member name"/>
            <Label>Avatar url:</Label>
            <Input bind:value={input.avatar_url} maxlength={256} type="url" placeholder={member.avatar_url} aria-label="member avatar url"/>
            <hr style="min-height: 1px" />
            <Label>Display name:</Label>
            <textarea class="form-control mb-2" style="resize: none; height: 1em" bind:value={input.display_name} maxlength={100} type="text" placeholder={member.display_name} aria-label="member display name" />
            <Label>Pronouns:</Label>
            <textarea class="form-control" style="resize: none; height: 1em" bind:value={input.pronouns} maxlength={100} type="text" placeholder={member.pronouns} aria-label="member pronouns" />
            <hr style="min-height: 1px" />
            <Label>Description:</Label>
            <textarea class="form-control" style="resize: none; overflow: hidden;" bind:value={input.description} maxlength={1000} use:autoresize placeholder={member.description} aria-label="member description"/>
            <hr style="min-height: 1px" />
            <Label>Color:</Label>
            <Row>
                <Col xs={9}>
                    <Input type="text" bind:value={input.color} aria-label="member color value"/>
                </Col>
                <Col class="p-0">
                    <Input class="p-0" on:change={(e) => input.color = e.target.value.slice(1, 7)} type="color" aria-label="color picker" />
                </Col>
            </Row>
            <hr style="min-height: 1px" />
            {#if err}
                {#each err as errorMessage}
                    <Alert class="m-0 mb-2" color="danger">{@html errorMessage}</Alert>
                {/each}
            {/if}
            <Row>
                <Col xs={4} class="align-items-center justify-content-center">
                    <Button disabled={loading} color="link" class="mt-2 text-danger" style="height: 3.5rem;" on:click={() => {view = "card"}} id={`member-${member.uuid}-back-button-edit`}><FaTimes/></Button>
                </Col>
                <Col xs={4}>
                </Col>
                <Col xs={4} class="align-items-center justify-content-center">
                    <Button disabled={loading} color="link" class="mt-2 text-success" style="height: 3.5rem;" on:click={submit} id={`member-${member.uuid}-submit-button-edit`}><FaCheck/></Button>
                </Col>
                <Tooltip target={`member-${member.uuid}-back-button-edit`} placement="bottom">Go back</Tooltip>
                <Tooltip target={`member-${member.uuid}-submit-button-edit`} placement="bottom">Submit edit</Tooltip>
            </Row>
        {/if}
    </div>
</div>

<style>
    .hide-scrollbar::-webkit-scrollbar {
        display: none;
    }

    .hide-scrollbar {
        scrollbar-width: none;
    }

    .item {
        height: auto;
    }

    @media (min-width: 768px) {
        .item {
            height: 30rem;
        }
    }
</style>