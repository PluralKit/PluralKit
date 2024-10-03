<script lang="ts">
  import { Card, CardImg, CardBody, Tooltip } from "sveltestrap"
  import type { Group } from "../../api/types"
  import resizeMedia from "../../api/resize-media";
  import default_avatar from '../../assets/default_avatar.png';
  import { useLocation } from "svelte-navigator";
  import FaLock from "svelte-icons/fa/FaLock.svelte";
  import FaUsers from "svelte-icons/fa/FaUserCircle.svelte"
  
  export let group: Group

  let location = useLocation()
    let pathName = $location.pathname;

  function getGroupPageUrl() {
    let str: string;
    if (pathName.startsWith("/dash")) str = "/dash";
    else str = "/profile";

    str += `/g/${group.id}`;

    return str;
  }
</script>

<a href={getGroupPageUrl()} class="card-link rounded flex-1 mb-3" >
  <Card style={`border: 3px solid #${group.color}`} class="h-100" >
    <CardImg style="border-bottom-right-radius: 0; border-bottom-left-radius: 0;" src={group.icon ? resizeMedia(group.icon, [256, 256], "webp") : default_avatar} />
    <CardBody class="text-center p-2 d-flex flex-col align-items-center justify-content-center">
      <h3>
        <button class="button-reset" style="width: auto; height: 1em; cursor: pointer; margin-right: 0.25em;" id={`m-copy-${group.uuid}`} >
          {#if group.privacy && group.privacy.visibility === "private"}
              <FaLock />
          {:else}
              <FaUsers />
          {/if}
        </button>
        {group.name} <span class="unbold">({group.id})</span>
      </h3>
    </CardBody>
  </Card>
</a>

<style>
  a.card-link, a.card-link:hover {
    color: unset;
    text-decoration: unset;
    display: inline-block;
    word-wrap: break-word;
    width: 100%;
  }

  a.card-link:hover {
    border: 4px solid var(--bs-primary);
    transform: scale(96%);
  }

  .unbold {
    font-weight: normal;
  }

  .button-reset {
    background: none;
    color: inherit;
    border: none;
    padding: 0;
    font: inherit;
    cursor: pointer;
    outline: inherit;
    }
</style>