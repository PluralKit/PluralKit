<script lang="ts">
  import { Col, Row, Tooltip } from "sveltestrap";
  import type { Member, Group } from "../../api/types"
  import type { ListOptions, PageOptions } from "./types";
  import { useLocation } from "svelte-navigator";
  import FaLock from "svelte-icons/fa/FaLock.svelte";
  import FaUserCircle from "svelte-icons/fa/FaUserCircle.svelte"
  import FaUsers from "svelte-icons/fa/FaUsers.svelte"
  import moment from 'moment';
  import parseMarkdown from '../../api/parse-markdown';
    import AwaitHtml from "../common/AwaitHtml.svelte";

  export let currentList: Member[] & Group[]
  export let pageOptions: PageOptions
  export let listOptions: ListOptions

  let copiedItems = {};

  function getShortLink(id: string) {
    let url = "https://pk.mt"

    if (pageOptions.type === "member") url += "/m/"
    else if (pageOptions.type === "group") url += "/g/"

    url += id;

    return url;
  }

  async function copyShortLink(index: string, id: string, event?) {
    if (event) {
      if (event.key !== "Tab") event.preventDefault();
      event.stopPropagation();

      let ctrlDown = event.ctrlKey||event.metaKey; // mac support
      if (!(ctrlDown && event.key === "c") && event.key !== "Enter") return;
    }
    try {
      await navigator.clipboard.writeText(getShortLink(id));
      
      copiedItems[index] = copiedItems[index] || false;
      copiedItems[index] = true;
      await new Promise(resolve => setTimeout(resolve, 2000));
      copiedItems[index] = false;
    } catch (error) {
      console.log(error);
    }
  }

  let location = useLocation()
  let pathName = $location.pathname;

  function getItemPageUrl(item: Member|Group) {
    let str: string;
    if (pathName.startsWith("/dash")) str = "/dash";
    else str = "/profile";

    str += pageOptions.type === "group" ? "/g" : "/m"
    str += `/${item.id}`;

    return str;
  }
</script>

<ol start={pageOptions.itemsPerPage * (pageOptions.currentPage - 1) + 1}>
  {#each currentList as item (item.uuid)}
    <li style="padding-left: 0.75rem">
      <Row class="justify-content-between">
        <Col xs={12} md="auto" class="d-flex align-items-center">
          <button class="button-reset" style="width: 1.1em; height: auto; cursor: pointer; display: flex; align-items: center;" id={`${pageOptions.type}-copy-${item.uuid}`} on:click|stopPropagation={() => copyShortLink(item.uuid, item.id)} on:keydown={(e) => copyShortLink(item.uuid, item.id, e)} tabindex={0} >
            {#if item.privacy && item.privacy.visibility === "private"}
              <FaLock />
            {:else if pageOptions.type === "group"}
              <FaUsers />
            {:else}
              <FaUserCircle />
            {/if}
          </button>
          <span><a class="list-link" href={getItemPageUrl(item)}><b>{item.name}</b></a> ({item.id})</span>
          <Tooltip placement="top" target={`${pageOptions.type}-copy-${item.uuid}`}>{copiedItems[item.uuid] ? "Copied!" : "Copy public link"}</Tooltip>
        </Col>
        {#if listOptions.extra && item[listOptions.extra]}
          <Col xs={12} md="auto" class="mt-2 mt-md-0">
          <div class="align-text">
            {#if ["avatar_url", "webhook_avatar_url", "icon", "banner"].some(i => i === listOptions.extra)}
              <a href={item[listOptions.extra]}>{item[listOptions.extra].slice(0, 50)}...</a>
            {:else if listOptions.extra === "birthday"}
              birthday - {moment(item.birthday, "YYYY-MM-DD").format("MMM D, YYYY").replace(', 0004', '')}
            {:else if listOptions.extra === "created"}
              created on {moment(item.created, "YYYY-MM-DD").format("MMM D, YYYY").replace(', 0004', '')}
            {:else if ["pronouns", "display_name"].some(i => i === listOptions.extra)}
              <AwaitHtml htmlPromise={parseMarkdown(item[listOptions.extra], { embed: true, parseTimestamps: true})}/>
            {:else if listOptions.extra === "color"}
              <div class="d-flex align-items-center">
                #{item.color}
                <div style={`width: 1em; height: 1em; border-radius: 2px; margin-left: 0.5rem; background-color: #${item.color}`}></div>
              </div>
            {:else}
              {item[listOptions.extra]}
            {/if}
          </div>
          </Col>
        {/if}
      </Row>
      <hr class="my-2"/>
    </li>
  {/each}
</ol>

<style>
  .list-link {
    text-decoration: none;
  }

  .list-link:hover {
    text-decoration: underline;
  }

  .button-reset {
    background: none;
    color: inherit;
    border: none;
    padding: 0;
    font: inherit;
    cursor: pointer;
    outline: inherit;
    display: inline-block;
    margin-right: 0.75rem;
  }

  .align-text {
    text-align: left; 
  }
  
  @media (min-width: 768px) {
    .align-text {
      text-align: right;
    }
  }
</style>