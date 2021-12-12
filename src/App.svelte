<script lang="ts">
  import { Router, Route } from "svelte-navigator";
  import Navigation from "./lib/Navigation.svelte";
  import Dash from "./pages/Dash.svelte";
  import Home from "./pages/Home.svelte";
  import Settings from './pages/Settings.svelte';
  import Footer from './lib/Footer.svelte';
  import Public from "./pages/Public.svelte";
  import System from "./pages/profiles/System.svelte";
  
  // theme cdns (I might make some myself too)
  let light = "https://cdn.jsdelivr.net/npm/bootstrap@5.1.0/dist/css/bootstrap.min.css";
  let dark = "https://cdn.jsdelivr.net/npm/bootstrap-dark-5@1.1.3/dist/css/bootstrap-night.min.css";

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
    <Route path="/dash"><Dash /></Route>
    <Route path="/settings"><Settings /></Route>
    <Route path="/public/*">
      <Route path="/"><Public /></Route>
      <Route path="/s/*">
        <Route path = "/:id">
          <System />
        </Route>
        <Route path = "/">
          hey please provide a system
        </Route>
      </Route>
      <Route path="/m/*">
        <Route path = "/:id">
          member
        </Route>
        <Route path = "/">
          hey please provide a member
        </Route>
      </Route>
      <Route path="/g/*">
        <Route path = "/:id">
          group
        </Route>
        <Route path = "/">
          hey please provide a group
        </Route>
      </Route>
    </Route>
  <Footer />
</Router>