<script lang="ts">
    import { Card, CardHeader, CardBody, Alert, Collapse, Row, Col, Spinner, Button, Tooltip } from 'sveltestrap';
    import { Member, Group } from '../../api/types';
    import { link } from 'svelte-navigator';

    import FaLock from 'svelte-icons/fa/FaLock.svelte';
    import FaUserCircle from 'svelte-icons/fa/FaUserCircle.svelte';
    import FaUsers from 'svelte-icons/fa/FaUsers.svelte'

    import MemberBody from '../member/Body.svelte';
    import GroupBody from '../group/Body.svelte';
    import CardsHeader from '../CardsHeader.svelte';

    let settings = JSON.parse(localStorage.getItem("pk-settings"));
    
    export let list: Member[]|Group[];
    export let members: Member[] = [];
    export let groups: Group[] = [];
    
    export let isPublic: boolean;
    export let itemType: string;
    export let isMainDash: boolean;
    
    let cardIndexArray = [];

    function getItemLink(item: Member | Group): string {
        let url: string;

        if (isMainDash) url = "/dash/";
        else url = "/profile/";
        
        if (itemType === "member") url += "m/";
        else if (itemType === "group") url += "g/";

        url += item.id;

        return url;
    }

    function skipToNextItem(event, index: number) {
        let el;

        if (event.key === "ArrowDown") {
            if (cardIndexArray[index + 1]) el = cardIndexArray[index + 1];
            else el = cardIndexArray[0];
        }

        if (event.key === "ArrowUp") {
            if (cardIndexArray[index - 1]) el = cardIndexArray[index - 1];
            else el = cardIndexArray[cardIndexArray.length - 1];
        }

        if (el) {
            event.preventDefault();
            el.focus();
        }
    }

    let isOpenArray = [];

    function toggleCard(index: number) {
        if (isOpenArray[index] === true) {
            isOpenArray[index] = false;
            cardIndexArray[index].classList.add("collapsed");
        } else {
            isOpenArray[index] = true;
            cardIndexArray[index].classList.remove("collapsed");
        }
    }
</script>

{#if settings && settings.accessibility ? (!settings.accessibility.expandedcards && !settings.accessibility.pagelinks) : true}
    <div class="mb-3">    
    {#each list as item, index (item.id + index)}
        <Card>
            <h2 class="accordion-header">
                <button class="w-100 accordion-button collapsed" bind:this={cardIndexArray[index]} on:click={() => toggleCard(index)} on:keydown={(e) => skipToNextItem(e, index)}>
                    <CardsHeader {item}>
                        <div slot="icon">
                            {#if isPublic || item.privacy.visibility === "public"}
                            {#if itemType === "member"}
                            <FaUserCircle />
                            {:else if itemType === "group"}
                            <FaUsers />
                            {/if}
                            {:else}
                            <FaLock />
                            {/if}
                        </div>
                    </CardsHeader>
                </button>
            </h2>
            <Collapse isOpen={isOpenArray[index]}>
                <CardBody>
                    {#if itemType === "member"}
                    <MemberBody on:deletion bind:isPublic bind:groups bind:member={item} />
                    {:else if itemType === "group"}
                    <GroupBody on:deletion {isPublic} {members} bind:group={item} />
                    {/if}
                </CardBody>
            </Collapse>
        </Card>
    {/each}
    </div>
{:else if settings.accessibility.expandedcards}
    {#each list as item, index (item.id + index)}
    <Card class="mb-3">
        <div class="accordion-button collapsed p-0" bind:this={cardIndexArray[index]} on:keydown={(e) => skipToNextItem(e, index)} tabindex={0}>
            <CardHeader class="w-100">
                <CardsHeader {item}>
                    <div slot="icon">
                        {#if isPublic || item.privacy.visibility === "public"}
                        {#if itemType === "member"}
                        <FaUserCircle />
                        {:else if itemType === "group"}
                        <FaUsers />
                        {/if}
                        {:else}
                        <FaLock />
                        {/if}
                    </div>
                </CardsHeader>
            </CardHeader>
        </div>
        <CardBody>
            {#if itemType === "member"}
            <MemberBody on:deletion bind:isPublic bind:groups bind:member={item} />
            {:else if itemType === "group"}
            <GroupBody on:deletion {isPublic} {members} bind:group={item} />
            {/if}
        </CardBody>
    </Card>
    {/each}
{:else}
    <div class="my-3">
    {#each list as item, index (item.id + index)}
    <Card>
        <a class="accordion-button collapsed" style="text-decoration: none;" href={getItemLink(item)} bind:this={cardIndexArray[index]} on:keydown={(e) => skipToNextItem(e, index)} use:link >
            <CardsHeader {item}>
                <div slot="icon">
                    {#if isPublic || item.privacy.visibility === "public"}
                    {#if itemType === "member"}
                    <FaUserCircle />
                    {:else if itemType === "group"}
                    <FaUsers />
                    {/if}
                    {:else}
                    <FaLock />
                    {/if}
                </div>
            </CardsHeader>
        </a>
    </Card>
    {/each}
    </div>
{/if}