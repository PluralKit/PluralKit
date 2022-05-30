<script lang="ts">
    import { tick } from 'svelte';
    import { Modal, CardHeader, CardTitle, Image, Spinner } from 'sveltestrap';
    import default_avatar from '../assets/default_avatar.png';
    import { toHTML } from 'discord-markdown';
    import twemoji from 'twemoji';
    import type { Group, Member, System } from '../api/types'; 

    export let item: any;

    let htmlName: string;
    let nameElement: any; 
    let settings = JSON.parse(localStorage.getItem("pk-settings"));

    $: if (item.name) htmlName = toHTML(item.name);
        else htmlName = "";

    $: if (settings && settings.appearance.twemoji) {
        if (nameElement) twemoji.parse(nameElement);
    }

    $: icon_url = item.avatar_url ? item.avatar_url : item.icon ? item.icon : default_avatar;

    let avatarOpen = false;
    const toggleAvatarModal = () => (avatarOpen = !avatarOpen);

    let altText = "icon";
    if (item.icon) altText = "group icon";
    else if (item.proxy_tags) altText = "member avatar";
    else if (item.tag) altText = "system avatar";
    
    export let loading: boolean = false;

    async function focus(el) {
        await tick();
        el.focus();
    }
</script>

    <CardTitle style="margin-top: 0px; margin-bottom: 0px; outline: none; align-items: center;" class="d-flex justify-content-between align-middle w-100">
        <div>
            <div class="icon d-inline-block">
                <slot name="icon" />
            </div>
            <span bind:this={nameElement} style="vertical-align: middle;">{@html htmlName} ({item.id})</span>
        </div>
        <div>
        {#if loading}
        <div class="d-inline-block mr-5" style="vertical-align: middle;"><Spinner color="primary" /></div>
        {/if}
        {#if item && (item.avatar_url || item.icon)}
        <img tabindex={0} on:keyup={(event) => {if (event.key === "Enter") avatarOpen = true}} on:click={toggleAvatarModal} class="rounded-circle avatar" src={icon_url} alt={altText} />
        {:else}
        <img class="rounded-circle avatar" src={default_avatar} alt="icon (default)" />
        {/if}
        </div>
        <Modal isOpen={avatarOpen} toggle={toggleAvatarModal}>
            <div slot="external" on:click={toggleAvatarModal} style="height: 100%;  max-width: 640px; width: 100%; margin-left: auto; margin-right: auto; display: flex;">
                <img class="d-block m-auto img-thumbnail" src={icon_url} alt={altText} tabindex={0} use:focus/>
            </div>
        </Modal>
    </CardTitle>