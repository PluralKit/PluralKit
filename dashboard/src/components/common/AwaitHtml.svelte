<script lang="ts">
    export let htmlPromise: Promise<string> = Promise.resolve("");
</script>

{#await htmlPromise}
    (loading...)
{:then html}
    {@html html ?? ""}
{:catch error}
    (failed to parse: {error?.message ?? String(error)})
{/await}