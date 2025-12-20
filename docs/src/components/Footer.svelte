<script lang="ts">
  import { env } from "$env/dynamic/public"

  // @ts-ignore
  const version = __COMMIT_HASH__.slice(1, __COMMIT_HASH__.length - 1)
</script>

<footer class="footer items-center p-4">
  <nav class="grid-flow-col gap-4">
    <span
      >Commit: <a
        aria-label="View commit on github"
        class="underline"
        href={`${
          env.PUBLIC_REPOSITORY_URL
            ? env.PUBLIC_REPOSITORY_URL
            : "https://github.com/Draconizations/pk-dashboard-sveltekit"
        }/commit/${version}`}>{version}</a
      >
    </span>
  </nav>
  <nav class="grid-flow-col gap-4 md:place-self-center md:justify-self-end">
    <a class="link-hover" href="/about">About</a>
    <a class="link-hover" href="/privacy">Privacy</a>
    <a class="link-hover" href="/changelog">Changelog</a>
  </nav>
</footer>
