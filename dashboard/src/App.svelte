<script lang="ts">
  import { Router, Route } from "svelte-navigator";
  import Navigation from "./lib/Navigation.svelte";
  import Dash from "./pages/Dash.svelte";
  import Home from "./pages/Home.svelte";
  import Settings from './pages/Settings.svelte';
  import Public from "./pages/Public.svelte";
  import Main from "./pages/Profile.svelte";
  import Status from './pages/status.svelte';
  import Member from './pages/Member.svelte';
  import Group from './pages/Group.svelte';
  import PageNotFound from './pages/PageNotFound.svelte';
  import { Alert } from 'sveltestrap';
import DiscordLogin from "./pages/DiscordLogin.svelte";
  import { onMount } from 'svelte';
import BulkGroupPrivacy from "./pages/BulkGroupPrivacy.svelte";
import BulkMemberPrivacy from "./pages/BulkMemberPrivacy.svelte";
  import Random from './pages/Random.svelte';
  
  // theme cdns (I might make some myself too)
  // if there's a style already set, retrieve it
  let style = localStorage.getItem("pk-style") && localStorage.getItem("pk-style");

  // this automatically applies the style every time it is updated
  $: setStyle(style);

  // not sure if there's a better way to handle this
  function setStyle(style) {
    switch (style) {
      case "light": document.documentElement.className = "light";
      localStorage.setItem("pk-style", "light");
      break;
      case "dark": document.documentElement.className = "dark";
      localStorage.setItem("pk-style", "dark");
      break;
      default: document.documentElement.className = "dark";
      localStorage.setItem("pk-style", "dark");
      break;
    };
  };

  onMount(() => {
    let settings = JSON.parse(localStorage.getItem("pk-settings"));

    if (settings && settings.accessibility && settings.accessibility.opendyslexic === true) {
      document.getElementById("app").classList.add("dyslexic");
    }
  });

</script>

<Router>
  <Navigation bind:style={style}/>
    <Route path="/"><Home /></Route>
    <Route path="/login/discord"><DiscordLogin /></Route>
    <Route path="dash"><Dash /></Route>
    <Route path="dash/m/:id"><Member isPublic={false}/></Route>
    <Route path = "dash/g/:id"><Group isPublic={false}/></Route>
    <Route path="dash/random"><Random isPublic={false} type={"member"}/></Route>
    <Route path="dash/random/m"><Random isPublic={false} type={"member"}/></Route>
    <Route path="dash/random/g"><Random isPublic={false} type={"group"}/></Route>
    <Route path="dash/g/:groupId/random"><Random isPublic={false} type={"member"} pickFromGroup={true}/></Route>
    <Route path="dash/bulk-member-privacy"><BulkMemberPrivacy/></Route>
    <Route path="dash/bulk-group-privacy"><BulkGroupPrivacy/></Route>
    <Route path="settings"><Settings /></Route>
    <Route path="profile"><Public /></Route>
    <Route path = "profile/s/:id"><Main /></Route>
    <Route path = "profile/s">
      <Alert color="danger">Please provide a system ID in the URL.</Alert>
    </Route>
    <Route path="profile/s/:id/random"><Random isPublic={true} type={"member"}/></Route>
    <Route path="profile/s/:id/random/m"><Random isPublic={true} type={"member"}/></Route>
    <Route path="profile/s/:id/random/g"><Random isPublic={true} type={"group"}/></Route>
    <Route path = "profile/m/:id"><Member/></Route>
    <Route path = "profile/m">
      <Alert color="danger">Please provide a member ID in the URL.</Alert>
    </Route>
    <Route path = "profile/g/:id"><Group/></Route>
    <Route path="profile/g/:groupId/random"><Random isPublic={true} type={"member"} pickFromGroup={true}/></Route>
    <Route path = "profile/g">
      <Alert color="danger">Please provide a group ID in the URL.</Alert>
    </Route>
    <Route path="status"><Status /></Route>
    <Route component={PageNotFound}/>
</Router>