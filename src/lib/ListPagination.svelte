<script lang="ts">
import { Pagination, PaginationItem, PaginationLink } from "sveltestrap";

export let currentPage: number;
export let pageAmount: number;
export let smallPages: boolean = false;

</script>
{#if pageAmount > 1}
<Pagination size={smallPages ? "sm" : ""} class="mx-auto" arialabel="member list page navigation">
    {#if currentPage !== 1}
    <PaginationItem>
        <PaginationLink href="#" previous on:click={(e) => {e.preventDefault(); currentPage -= 1}}></PaginationLink>
    </PaginationItem>
    {:else}
    <PaginationItem disabled>
        <PaginationLink previous></PaginationLink>
    </PaginationItem>
    {/if}
    {#if currentPage > 2}
    <PaginationItem>
        <PaginationLink href="#" on:click={(e) => {e.preventDefault(); currentPage = 1}}>1</PaginationLink>
    </PaginationItem>
    {/if}
    {#if currentPage === 4}
        <PaginationItem>
            <PaginationLink href="#" on:click={(e) => {e.preventDefault(); currentPage = 2}}>2</PaginationLink>
        </PaginationItem>
    {/if}
    {#if currentPage > 4}
    <PaginationItem disabled>
        <PaginationLink>...</PaginationLink>
    </PaginationItem>
    {/if}
    {#if currentPage > 1}
        <PaginationItem>
            <PaginationLink href="#" on:click={(e) => {e.preventDefault(); currentPage -= 1}}>{currentPage - 1}</PaginationLink>
        </PaginationItem>
    {/if}
        <PaginationItem active>
            <PaginationLink href="#">{currentPage}</PaginationLink>
        </PaginationItem>
    {#if currentPage < pageAmount}
        <PaginationItem>
            <PaginationLink href="#" on:click={(e) => {e.preventDefault(); currentPage += 1}}>{currentPage + 1}</PaginationLink>
        </PaginationItem>
    {/if}
    {#if currentPage < pageAmount - 3}
        <PaginationItem disabled>
            <PaginationLink>...</PaginationLink>
        </PaginationItem>
    {/if}
    {#if currentPage === pageAmount - 3}
        <PaginationItem>
            <PaginationLink href="#" on:click={(e) => {e.preventDefault(); currentPage = pageAmount - 1}}>{pageAmount - 1}</PaginationLink>
        </PaginationItem>
    {/if}
    {#if currentPage < pageAmount - 1}
        <PaginationItem>
            <PaginationLink href="#" on:click={(e) => { e.preventDefault(); currentPage = pageAmount}}>{pageAmount}</PaginationLink>
        </PaginationItem>
    {/if}
    {#if currentPage !== pageAmount}
        <PaginationItem>
            <PaginationLink href="#" next on:click={(e) => {e.preventDefault(); currentPage += 1}}></PaginationLink>
        </PaginationItem>
    {:else}
        <PaginationItem disabled>
            <PaginationLink next></PaginationLink>
        </PaginationItem>
    {/if}
</Pagination>
{/if}