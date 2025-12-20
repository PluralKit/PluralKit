<script lang="ts">
  import { browser } from "$app/environment"
  import NavBar from "$components/NavBar.svelte"
  import Sidebar from "$components/Sidebar.svelte"
  import "$lib/app.scss"
  import "$lib/nprogress.scss"
  import type { LayoutData } from "./$types"
  import Footer from "$components/Footer.svelte"
  import { page } from "$app/stores"
  import { navigating } from "$app/stores"
  import nprogress from "nprogress"
//   import apiClient from "$api"

  export let data: LayoutData

//   if (browser) {
//     window.api = apiClient(fetch, data.apiBaseUrl)
//   }

  if (data.token && browser) {
    localStorage.setItem("pk-token", data.token)
  } else if (browser) {
    localStorage.removeItem("pk-token")
  }

  nprogress.configure({
    parent: "#themed-container",
  })

  $: {
    if ($navigating) nprogress.start()
    else if (!$navigating) nprogress.done()
  }

//   dash.initUser(data.system)
</script>

<div
  id="themed-container"
  class="max-w-screen h-screen bg-base-100 flex flex-col"
  data-theme="coffee"
>
  <NavBar />
  <div class="flex flex-row flex-1 min-h-0">
    <Sidebar />
    <main class="flex-1 overflow-y-auto min-h-0">
      <slot />
    </main>
  </div>
  <Footer />
</div>

<svelte:head>
  <title>PluralKit | {$page.data?.meta?.title ?? "Home"}</title>
  <meta
    property="og:title"
    content={`PluralKit | ${$page.data?.meta?.ogTitle ?? "Web Dashboard"}`}
  />
  <meta property="theme-color" content={`#${$page.data?.meta?.color ?? "da9317"}`} />
  <meta
    property="og:description"
    content={$page.data?.meta?.ogDescription ?? "PluralKit's official dashboard."}
  />
</svelte:head>
