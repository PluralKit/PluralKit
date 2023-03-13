<script lang="ts">
    import { tick } from 'svelte';
    import { Modal, CardTitle} from 'sveltestrap';
    import AwaitHtml from './AwaitHtml.svelte';
    import default_avatar from '../../assets/default_avatar.png';
    import resizeMedia from '../../api/resize-media';
    import parseMarkdown from '../../api/parse-markdown';
    import twemoji from 'twemoji';

    export let item: any;
    export let searchBy: string = null;
    export let sortBy: string = null;

    let htmlNamePromise: Promise<string>;
    let nameElement: any;
    let settings = JSON.parse(localStorage.getItem("pk-settings"));

    $: if (item.name) {
        if ((searchBy === "display_name" || sortBy === "display_name") && item.display_name) htmlNamePromise = parseMarkdown(item.display_name);
        else htmlNamePromise = parseMarkdown(item.name);
    }

    $: if (settings && settings.appearance.twemoji) {
        if (nameElement) twemoji.parse(nameElement, { base: 'https://cdn.jsdelivr.net/gh/twitter/twemoji@14.0.2/assets/' });
    }

    $: icon_url = item.avatar_url ? item.avatar_url : item.icon ? item.icon : default_avatar;
    $: icon_url_resized = resizeMedia(icon_url)

    let avatarOpen = false;
    const toggleAvatarModal = () => (avatarOpen = !avatarOpen);

    // this is the easiest way we can check what type of item the header has
    // unsure if there's a better way
    let altText = "icon";
    if (item.icon) altText = item.name ? `group ${item.name} icon (full size)` : "group icon (full size)";
    else if (item.proxy_tags) altText = item.name ? `member ${item.name} avatar (full size)` : "member avatar (full size)";
    else if (item.tag) altText = item.name ? `system ${item.name} avatar (full size)` : "system avatar (full size)";

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
            <span bind:this={nameElement} style="vertical-align: middle;"><AwaitHtml htmlPromise={htmlNamePromise} /> ({item.id})</span>
        </div>
        <div style="margin-left: auto;">
        {#if item && (item.avatar_url || item.icon)}
        <img tabindex={0} on:keydown|stopPropagation={(event) => {if (event.key === "Enter") {avatarOpen = true}}} on:click|stopPropagation={toggleAvatarModal} class="rounded-circle avatar" src={icon_url_resized} alt={altText} />
        {:else}
        <img class="rounded-circle avatar" src={default_avatar} alt="icon (default)" tabindex={0} />
        {/if}
        </div>
        <Modal isOpen={avatarOpen} toggle={toggleAvatarModal}>
            <div slot="external" on:click={toggleAvatarModal} style="height: 100%;  max-width: 640px; width: 100%; margin-left: auto; margin-right: auto; display: flex;">
                <img class="d-block m-auto img-thumbnail" src={icon_url} alt={altText} tabindex={0} use:focus/>
            </div>
        </Modal>
    </CardTitle>
