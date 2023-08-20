<script lang="ts">
    import { Card, CardImg, Row, Col } from "sveltestrap";
    import type { Group, Member } from "../../api/types";
    import type { PageOptions } from "./types";
    import default_avatar from '../../assets/default_avatar.png';
    import resizeMedia from "../../api/resize-media";
    import TinyMemberView from "../member/TinyMemberView.svelte";
    import TinyGroupView from "../group/TinyGroupView.svelte";

  export let pageOptions: PageOptions;
  export let currentList: Member[]|Group[];
  $: memberList = currentList as Member[]
  $: groupList = currentList as Group[] 
</script>

<Row class="mx-4 mx-sm-5 mx-md-0">
  {#if pageOptions.type === "member"}
    {#each memberList as item (item.uuid)}
    <Col xs={6} md={4} lg={3} xl={2} class="d-flex flex-col">
      <TinyMemberView member={item} />
    </Col>
    {/each}
  {:else if pageOptions.type === "group"}
    {#each groupList as item (item.uuid)}
    <Col xs={6} md={4} lg={3} xl={2} class="d-flex flex-col">
      <TinyGroupView group={item} />
    </Col>
    {/each}
  {/if}
</Row>