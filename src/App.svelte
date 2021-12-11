<script lang="ts">
  import { onMount } from "svelte";
  import { Router, Link, Route } from "svelte-navigator";
  import Navigation from "./lib/Navigation.svelte"; 
  

  // theme cdns (I might make some myself too)
  let light = "https://cdn.jsdelivr.net/npm/bootstrap@5.1.0/dist/css/bootstrap.min.css";
  let dark = "https://cdn.jsdelivr.net/npm/bootstrap-dark-5@1.1.3/dist/css/bootstrap-night.min.css";

  let styleSrc = dark;

  onMount(() => {
    if (localStorage.getItem("pk-style")) setStyle(localStorage.getItem("pk-style").toLowerCase());
  });

  function styleEventHandler(event) {
    let style = event.detail;
    setStyle(style);
  }

  function setStyle(style) {
    
    switch (style) {
      case "light": styleSrc = light;
      localStorage.setItem("pk-style", "light");
      break;
      case "dark": styleSrc = dark;
      localStorage.setItem("pk-style", "dark");
      break;
      default: styleSrc = light;
      localStorage.setItem("pk-style", "light");
      default: styleSrc = dark;
      localStorage.setItem("pk-style", "dark");
    };
  };
</script>

<svelte:head>
  <link rel="stylesheet" href={styleSrc}>
</svelte:head>

<Router>
  <Navigation on:styleChange={styleEventHandler}/>
  <div>
    <Route path="/">
        <h2>Ooga booga</h2>
    </Route>
  </div>
</Router>