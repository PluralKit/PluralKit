<script lang="ts">
    import { Modal, CardHeader, CardTitle, Image, Spinner } from 'sveltestrap';
    import FaUserCircle from 'svelte-icons/fa/FaUserCircle.svelte'
    import default_avatar from '../assets/default_avatar.png';

    export let item: any;

    let icon_url = item.avatar_url ? item.avatar_url : item.icon ? item.icon : default_avatar;

    let avatarOpen = false;
    const toggleAvatarModal = () => (avatarOpen = !avatarOpen);
    
    export let loading: boolean;
</script>

    <CardTitle style="margin-top: 0px; margin-bottom: 0px; outline: none; align-items: center;" class="d-flex justify-content-between align-middle w-100">
        <div>
            <div class="icon d-inline-block">
                <slot name="icon" />
            </div>
            <span style="vertical-align: middle;">{item.name} ({item.id})</span>
        </div>
        <div>
        {#if loading}
        <div class="d-inline-block mr-5" style="vertical-align: middle;"><Spinner color="primary" /></div>
        {/if}
        {#if item && (item.avatar_url || item.icon)}
        <img tabindex={0} on:keyup={(event) => {if (event.key === "Enter") avatarOpen = true}} on:click={toggleAvatarModal} class="rounded-circle avatar" src={icon_url} alt="Icon" />
        {:else}
        <img class="rounded-circle avatar" src={default_avatar} alt="your system avatar (default)" />
        {/if}
        </div>
        <Modal isOpen={avatarOpen} toggle={toggleAvatarModal}>
            <div slot="external" on:click={toggleAvatarModal} style="height: 100%;  max-width: 640px; width: 100%; margin-left: auto; margin-right: auto; display: flex;">
                <Image style="display: block; margin: auto;" src={icon_url} thumbnail alt="Your system avatar" />
            </div>
        </Modal>
    </CardTitle>