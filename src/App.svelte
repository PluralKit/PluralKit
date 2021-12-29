<script lang="ts">
  import { Router, Route } from "svelte-navigator";
  import Navigation from "./lib/Navigation.svelte";
  import Dash from "./pages/Dash.svelte";
  import Home from "./pages/Home.svelte";
  import Settings from './pages/Settings.svelte';
  import Footer from './lib/Footer.svelte';
  import Public from "./pages/Public.svelte";
  import Main from "./pages/profiles/Main.svelte";
  
  // theme cdns (I might make some myself too)
  let light = "/styles/light.scss";
  let dark = "/styles/dark.scss";

  let styleSrc = dark;

  // if there's a style already set, retrieve it
  let style = localStorage.getItem("pk-style") && localStorage.getItem("pk-style");

  // this automatically applies the style every time it is updated
  $: setStyle(style);

  // not sure if there's a better way to handle this
  function setStyle(style) {
    switch (style) {
      case "light": styleSrc = light;
      localStorage.setItem("pk-style", "light");
      break;
      case "dark": styleSrc = dark;
      localStorage.setItem("pk-style", "dark");
      break;
      default: styleSrc = dark;
      localStorage.setItem("pk-style", "dark");
    };
  };

</script>

<svelte:head>
  <link rel="stylesheet" href={styleSrc}>
</svelte:head>

<Router>
  <Navigation bind:style={style}/>
    <Route path="/"><Home /></Route>
    <Route path="dash"><Dash /></Route>
    <Route path="settings"><Settings /></Route>
    <Route path="profile"><Public /></Route>
    <Route path = "profile/s/:id"><Main /></Route>
    <Route path = "s">
      hey please provide a system
    </Route>
    <Route path = "profile/m/:id">
      member
    </Route>
    <Route path = "profile/m">
      hey please provide a member
    </Route>
    <Route path = "profile/g/:id">
      group!
    </Route>
    <Route path = "profile/g">
      hey please provide a group
    </Route>
  <Footer />
</Router>